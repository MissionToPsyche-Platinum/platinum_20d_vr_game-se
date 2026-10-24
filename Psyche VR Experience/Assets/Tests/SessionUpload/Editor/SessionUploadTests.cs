using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

namespace PsycheVR.Tests
{
    // Runtime scripts live in Assembly-CSharp. Resolve them at runtime because a
    // test asmdef cannot reference Unity's predefined assemblies directly.
    public sealed class SessionUploadTests
    {
        private string directory;
        private Type store;

        [SetUp]
        public void SetUp()
        {
            store = Type.GetType("PsycheVR.Data.SessionUploadStore, Assembly-CSharp", true);
            directory = Path.Combine(Path.GetTempPath(), "psyche-upload-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(directory, true);

        private object Call(string method, params object[] args)
        {
            try { return store.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args); }
            catch (TargetInvocationException error)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        private string Log(string name = "one", string content = "log data")
        {
            string path = Path.Combine(directory, "session_" + name + ".jsonl");
            File.WriteAllText(path, content);
            return path;
        }

        private string Hash(string path) => (string)Call("Hash", File.ReadAllText(path));
        private string Archived(string path, string folder) => Path.Combine(directory, folder, Path.GetFileName(path));
        private string[] Pending() => (string[])Call("Files", directory);

        [Test]
        public void ArchivedFilesAreExcludedFromPendingEnumeration()
        {
            string sent = Log("sent");
            string rejected = Log("rejected");
            Call("ArchiveAcknowledged", sent, null, "{\"ok\":true}", Hash(sent));
            Call("Archive", rejected, null, true, null);
            string pending = Log("pending");
            // Even unreadable archived files must not be opened during enumeration.
            using (File.Open(Archived(sent, "Sent"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (File.Open(Archived(rejected, "Rejected"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.That(Pending(), Is.EqualTo(new[] { pending }));
        }

        [TestCase(null)]
        [TestCase("{\"ok\":false}")]
        [TestCase("{\"ok\":\"true\"}")]
        [TestCase("<html>error</html>")]
        public void UnconfirmedLogsStayPending(string response)
        {
            string path = Log();
            Assert.That(Call("ArchiveAcknowledged", path, null, response, Hash(path)), Is.False);
            Assert.That(Pending(), Is.EqualTo(new[] { path }));
            Assert.That(File.Exists(path + ".sent"), Is.False);
        }

        [Test]
        public void ConfirmationArchivesTheLogAndReceipt()
        {
            string path = Log();
            string hash = Hash(path);
            Assert.That(Call("ArchiveAcknowledged", path, null, "{\"ok\":true}", hash), Is.True);
            Assert.That(Pending(), Is.Empty);
            Assert.That(File.ReadAllText(Archived(path, "Sent")), Is.EqualTo("log data"));
            Assert.That(File.ReadAllText(Archived(path, "Sent") + ".sent"), Is.EqualTo(hash));
        }

        [Test]
        public void ActiveLogNeverMovesEvenWithConfirmationOrLegacyReceipt()
        {
            string path = Log();
            string hash = Hash(path);
            Call("MarkSent", path, hash);
            Assert.That(Call("ArchiveAcknowledged", path, path, "{\"ok\":true}", hash), Is.False);
            Assert.That(Call("MigrateLegacy", path, path, hash), Is.False);
            Assert.That(Call("Archive", path, path, true, null), Is.False);
            Assert.That(Pending(), Is.EqualTo(new[] { path }));
        }

        [Test]
        public void InterruptedMoveLeavesLogPendingForResend()
        {
            string path = Log();
            string hash = Hash(path);
            // Simulate a crash after the archive receipt is written but before moving the log.
            Directory.CreateDirectory(Path.Combine(directory, "Sent"));
            Call("MarkSent", Archived(path, "Sent"), hash);
            Assert.That(Call("MigrateLegacy", path, null, hash), Is.False);
            Assert.That(Pending(), Is.EqualTo(new[] { path }));
            Assert.That(File.ReadAllText(path), Is.EqualTo("log data"));
            // The next confirmed upload recovers without overwriting the orphan receipt.
            Assert.That(Call("ArchiveAcknowledged", path, null, "{\"ok\":true}", hash), Is.True);
            Assert.That(Pending(), Is.Empty);
            Assert.That(Directory.GetFiles(Path.Combine(directory, "Sent"), "*.jsonl"), Has.Length.EqualTo(1));
        }

        [Test]
        public void LegacyReceiptMigratesOnceButStaleReceiptDoesNot()
        {
            string path = Log();
            string hash = Hash(path);
            Call("MarkSent", path, hash);
            Assert.That(Call("MigrateLegacy", path, null, hash), Is.True);
            Assert.That(File.Exists(path + ".sent"), Is.False);
            Assert.That(File.ReadAllText(Archived(path, "Sent") + ".sent"), Is.EqualTo(hash));
            Assert.That(Pending(), Is.Empty);
            string grown = Log("grown");
            Call("MarkSent", grown, Hash(grown));
            File.AppendAllText(grown, "more events");
            Assert.That(Call("MigrateLegacy", grown, null, Hash(grown)), Is.False);
            Assert.That(Pending(), Is.EqualTo(new[] { grown }));
        }

        [Test]
        public void OversizedLogIsPreservedInRejectedAfterItCloses()
        {
            string path = Log(content: new string('a', 1000000));
            Assert.That(((string)Call("Read", path)).Length, Is.EqualTo(1000000));
            File.AppendAllText(path, "a");
            Assert.That(() => Call("Read", path), Throws.TypeOf(store.GetNestedType("OversizedLogException")));
            Assert.That(Call("Archive", path, path, true, null), Is.False);
            Assert.That(Call("Archive", path, null, true, null), Is.True);
            Assert.That(new FileInfo(Archived(path, "Rejected")).Length, Is.EqualTo(1000001));
            Assert.That(Pending(), Is.Empty);
        }

        [Test]
        public void StorageStatusCountsOnlyLogsInEachFolder()
        {
            string path = Log("sent");
            Call("ArchiveAcknowledged", path, null, "{\"ok\":true}", Hash(path));
            Call("Archive", Log("rejected"), null, true, null);
            Log("current");
            File.WriteAllText(Path.Combine(directory, "unrelated.jsonl"), "");
            Type status = Type.GetType("PsycheVR.Data.SessionStorageStatus, Assembly-CSharp", true);
            string details = (string)status.GetMethod("Details").Invoke(null, new object[] { directory });
            Assert.That(details, Does.StartWith("freeBytes="));
            Assert.That(details, Does.EndWith(";waiting=1;sent=1;rejected=1"));
        }
    }
}
