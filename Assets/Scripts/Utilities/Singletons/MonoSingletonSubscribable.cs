using UnityEngine;
using System.Collections.Generic;

public class MonoSingletonSubscribable<T, U> : MonoSingleton<U> where U : MonoBehaviour
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