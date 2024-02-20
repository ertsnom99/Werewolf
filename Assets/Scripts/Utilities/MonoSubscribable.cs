using System.Collections.Generic;
using UnityEngine;

public abstract class MonoSubscribable<T> : MonoBehaviour
{
	protected List<T> Subscribers = new();

	public virtual void Subscribe(T subscriber)
	{
		Subscribers.Add(subscriber);
	}

	public virtual void Unsubscribe(T subscriber)
	{
		Subscribers.Remove(subscriber);
	}
}