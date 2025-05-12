using System.Collections.Generic;
using UnityEngine;

public abstract class MonoSubscribable<T> : MonoBehaviour
{
	protected List<T> Subscribers = new();

	public virtual void Subscribe(T subscriber, int priority = -1)
	{
		if (!Subscribers.Contains(subscriber))
		{
			if (priority > 1 && priority <= Subscribers.Count)
			{
				Subscribers.Insert(priority, subscriber);
			}
			else
			{
				Subscribers.Add(subscriber);
			}
		}
	}

	public virtual void Unsubscribe(T subscriber)
	{
		if (Subscribers.Contains(subscriber))
		{
			Subscribers.Remove(subscriber);
		}
	}
}