using UnityEngine;
using System.Collections;

// From https://forum.unity.com/threads/singleton-monobehaviour-script.99971/
public class MonoSingleton<T> where T : MonoBehaviour
{
    private static T _instance;
    private static bool isFound;
    private bool createMissingInstance;

    static MonoSingleton()
    {
        isFound = false;
        _instance = null;
    }

    public MonoSingleton(bool createNewInstanceIfNeeded = true)
    {
        this.createMissingInstance = createNewInstanceIfNeeded;
    }

    public T Instance
    {
        get
        {
            if (isFound && _instance)
            {
                return _instance;
            }
            else
            {
                UnityEngine.Object[] objects = GameObject.FindObjectsOfType(typeof(T));
                if (objects.Length > 0)
                {
                    if (objects.Length > 1)
                        Debug.LogWarning(objects.Length + " " + typeof(T).Name + "s were found! Make sure to have only one at a time!");
                    isFound = true;
                    _instance = (T)System.Convert.ChangeType(objects[0], typeof(T));
                    return _instance;
                }
                else
                {
                    Debug.LogError(typeof(T).Name + " script cannot be found in the scene!!!");
                    if (createMissingInstance)
                    {
                        GameObject newInstance = new GameObject(typeof(T).Name);
                        isFound = true;
                        _instance = newInstance.AddComponent<T>();
                        Debug.Log(typeof(T).Name + " was added to the root of the scene");
                        return _instance;
                    }
                    else
                    {
                        isFound = false;
                        return null; // or default(T)
                    }
                }
            }
        }
    }
}
