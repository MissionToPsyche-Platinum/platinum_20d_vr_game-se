using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SnapZone : MonoBehaviour
{
    public string requiredTag;
    public Transform snapAnchor;
    public Renderer zoneRenderer;
    public Material validMat;
    public Material invalidMat;
    public bool hasSnapped;
    public InstructionTextManager textManager;

    private XRGrabInteractable currentObject;

    private const string BusZoneTag = "bus_SnapZone";

    void Start()
    {
        // Only the bus snap zone starts active.
        if (!CompareTag(BusZoneTag))
        {
            this.enabled = false;
            return;
        }

        if (zoneRenderer != null)
            zoneRenderer.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasSnapped || !this.enabled) return;
        if (other.attachedRigidbody == null) return;

        XRGrabInteractable grab = other.attachedRigidbody.GetComponent<XRGrabInteractable>();
        if (grab == null || !grab.enabled) return;

        SnappableObject snapState = other.attachedRigidbody.GetComponent<SnappableObject>();
        if (snapState != null && snapState.isSnapped) return;

        currentObject = grab;

        if (zoneRenderer != null)
        {
            zoneRenderer.enabled = true;
            SetZoneMaterial(grab.CompareTag(requiredTag) ? validMat : invalidMat);
        }

        grab.selectExited.AddListener(OnReleased);
    }

    private void OnTriggerExit(Collider other)
    {
        if (hasSnapped) return;
        if (other.attachedRigidbody == null) return;

        XRGrabInteractable grab = other.attachedRigidbody.GetComponent<XRGrabInteractable>();
        if (grab == null) return;

        if (currentObject == grab)
        {
            grab.selectExited.RemoveListener(OnReleased);
            currentObject = null;

            if (zoneRenderer != null)
                zoneRenderer.enabled = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        XRGrabInteractable grab = args.interactableObject as XRGrabInteractable;
        if (grab == null) return;
        if (grab != currentObject) return;
        if (!grab.CompareTag(requiredTag)) return;

        StartCoroutine(SnapNextFrame(grab));
    }

    private IEnumerator SnapNextFrame(XRGrabInteractable grab)
    {
        yield return null;

        SnappableObject snapState = grab.GetComponent<SnappableObject>();
        if (snapState != null && snapState.isSnapped) yield break;

        Rigidbody rb = grab.GetComponent<Rigidbody>();

        if (snapState != null)
        {
            snapState.SnapTo(snapAnchor);
            snapState.isSnapped = true;
        }
        else
        {
            grab.transform.position = snapAnchor.position;
            grab.transform.rotation = snapAnchor.rotation;
        }

        grab.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        hasSnapped = true;

        if (zoneRenderer != null)
            zoneRenderer.enabled = false;

        grab.selectExited.RemoveListener(OnReleased);

        if (CompareTag(BusZoneTag))
        {
            EnableAllOtherSnapZones();
        }

        if (textManager != null && snapState != null)
        {
            textManager.SetText(snapState.objectDescription);

        }

        currentObject = null;
    }

    private void EnableAllOtherSnapZones()
    {
        SnapZone[] allZones = FindObjectsByType<SnapZone>(FindObjectsSortMode.None);

        foreach (SnapZone zone in allZones)
        {
            if (zone == null) continue;
            if (zone == this) continue;

            zone.enabled = true;

            if (zone.zoneRenderer != null)
                zone.zoneRenderer.enabled = false;
        }
    }

    private void SetZoneMaterial(Material mat)
    {
        if (zoneRenderer == null || mat == null) return;

        Material[] mats = new Material[zoneRenderer.sharedMaterials.Length];

        for (int i = 0; i < mats.Length; i++)
        {
            mats[i] = mat;
        }

        zoneRenderer.materials = mats;
    }
}
