﻿using System.Globalization;

namespace Console.TypeReader
{
	/// <summary>
	/// A default reader for <see cref="int"/>
	/// </summary>
	public sealed class IntReader : Console.TypeReader.ITypeReader
	{
		public object ReadType(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return 0;

			return int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out int result) ? result : 0;
		}
	}
}