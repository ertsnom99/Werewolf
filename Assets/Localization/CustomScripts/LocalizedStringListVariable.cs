using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace UnityEngine.Localization.SmartFormat.PersistentVariables
{
	// From: https://forum.unity.com/threads/using-list-formatter-for-an-array-of-localizedstring.1394401/
	[Serializable]
	public class LocalizedStringListVariable : IVariable
	{
		public List<LocalizedString> Values = new();

		public object GetSourceValue(ISelectorInfo selector)
		{
			return Values.Select(l => l.GetLocalizedString()).ToList();
		}
	}
}