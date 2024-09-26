﻿using UnityEngine;

/// <summary>
/// WeakMonoSingleton
/// </summary>
/// <remarks> 
/// Any class extending this class becomes a Singleton and a MonoBehaviour.
/// A WeakReference instance is used to store the instance.
/// T is the class that extends this singleton.
/// This is a lazy singleton, therefore it will only be created the first time someone tries to access it.
/// </remarks>
public class WeakMonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
	private static System.WeakReference _instance = null;
	private static readonly object _lock = new();
	private static bool _applicationIsQuitting = false;

	public static T Instance
	{
		get
		{
			if (_applicationIsQuitting)
			{
				Debug.LogWarning("[Singleton] Instance '" + typeof(T) + "' already destroyed on application quit. Won't create again - returning null.");
				return null;
			}

			lock (_lock)
			{
				// Check if either the WeakReference exists or if its target/alive state isn't correct
				if (_instance == null || (T)_instance.Target == null || !_instance.IsAlive)
				{
					if (_instance == null)
					{
						_instance = new((T)FindObjectOfType(typeof(T)));
					}
					else
					{
						_instance.Target = (T)FindObjectOfType(typeof(T));
					}

					if (FindObjectsOfType(typeof(T)).Length > 1)
					{
						Debug.LogError("[Singleton] Something went really wrong - there should never be more than 1 singleton! Reopening the scene might fix it.");
						return _instance.Target as T;
					}

					if ((T)_instance.Target == null || !_instance.IsAlive)
					{
						GameObject singleton = new();
						_instance.Target = singleton.AddComponent<T>();
						singleton.name = "(singleton) " + typeof(T).ToString();

						Debug.Log("[Singleton] An instance of " + typeof(T) + " is needed in the scene, so '" + singleton + "' was created.");
					}
					else
					{
						Debug.Log("[Singleton] Using instance already created: " + (_instance.Target as T).gameObject.name);
					}
				}

				return _instance.Target as T;
			}
		}
	}

	/// <summary>
	/// When Unity quits, it destroys objects in a random order.
	/// In principle, a Singleton is only destroyed when application quits.
	/// If any script calls Instance after it has been destroyed, 
	///   it will create a buggy ghost object that will stay on the Editor scene
	///   even after the Application stopped playing.
	/// So, this was made to be sure we're not creating that buggy ghost object.
	/// </summary>
	public void OnApplicationQuit()
	{
		_applicationIsQuitting = true;
	}

	protected virtual void Awake()
	{
		if (_instance == null)
		{
			_instance = new(this);
		}
		else if ((T)_instance.Target != null && _instance.IsAlive && (T)_instance.Target != this)
		{
			Destroy(gameObject);
		}
	}
}