using UnityEngine;
using UnityEngine.Events;

public class Initiate_Launch : MonoBehaviour
{
    [SerializeField] private UnityEvent onLaunchInitiated = new UnityEvent();

    public void InitiateLaunch()
    {
        onLaunchInitiated.Invoke();
    }
}
