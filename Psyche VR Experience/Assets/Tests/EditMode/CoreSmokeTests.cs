using NUnit.Framework;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>Confirms the test runner discovers and runs this assembly.</summary>
    public class CoreSmokeTests
    {
        /// <summary>Passes whenever the assembly is found and compiled.</summary>
        [Test]
        public void TestRunnerFindsThisAssembly() => Assert.Pass();
    }
}
