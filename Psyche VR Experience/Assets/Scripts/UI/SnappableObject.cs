using System;
using UnityEngine;

/// <summary>
/// Lightweight state tracker for objects that can snap into SnapZones.
/// Fires onSnapped event when the piece is locked in place.
/// </summary>
public class SnappableObject : MonoBehaviour
{
    private bool _isSnapped;

    /// <summary>
    /// Fired once when the object snaps into a zone. Used by ComponentInfo
    /// to trigger chalkboard update without polling.
    /// </summary>
    public event Action onSnapped;

    public bool isSnapped
    {
        get => _isSnapped;
        set
        {
            if (_isSnapped == value) return;
            _isSnapped = value;
            if (_isSnapped)
                onSnapped?.Invoke();
        }
    }
}
