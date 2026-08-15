using UnityEngine;
using UnityEngine.Events;

public class MethodCaller : MonoBehaviour
{
    public UnityEvent Call;

    public void CallMethod()
    {
        Call?.Invoke();
    }
}