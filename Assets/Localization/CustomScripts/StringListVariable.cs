using System;
using System.Collections.Generic;
using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace UnityEngine.Localization.SmartFormat.PersistentVariables
{
	[Serializable]
	public class StringListVariable : IVariable
	{
		public List<string> Values = new();

		public object GetSourceValue(ISelectorInfo selector)
		{
			return Values;
		}
	}
}