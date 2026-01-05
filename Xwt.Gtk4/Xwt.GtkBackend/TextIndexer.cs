using System.Collections.Generic;
using System.Text;

namespace Xwt.GtkBackend
{
	public class TextIndexer
	{
		static readonly List<int> EmptyList = new List<int>();
		static readonly int[] EmptyArray = new int[0];
		int[] indexToByteIndex;
		List<int> byteIndexToIndex;
		string text = string.Empty;

		public TextIndexer(string text)
		{
			SetupTables(text);
		}

		public int IndexToByteIndex(int i)
		{
			if (indexToByteIndex.Length == 0)
				return 0;
			if (i >= indexToByteIndex.Length)
				return indexToByteIndex[indexToByteIndex.Length - 1] + 1;
			if (i < 0)
				return 0;
			return indexToByteIndex[i];
		}

		public int ByteIndexToIndex(int i)
		{
			if (byteIndexToIndex.Count == 0)
				return 0;
			if (i < 0)
				return 0;
			if (i >= byteIndexToIndex.Count)
				return byteIndexToIndex[byteIndexToIndex.Count - 1];
			return byteIndexToIndex[i];
		}

		public void SetupTables(string text)
		{
			this.text = text ?? string.Empty;
			if (string.IsNullOrEmpty(this.text)) {
				indexToByteIndex = EmptyArray;
				byteIndexToIndex = EmptyList;
				return;
			}

			int byteIndex = 0;
			indexToByteIndex = new int[this.text.Length];
			byteIndexToIndex = new List<int>(this.text.Length);

			for (int i = 0; i < this.text.Length; i++) {
				indexToByteIndex[i] = byteIndex;
				var bytes = Encoding.UTF8.GetByteCount(this.text.Substring(i, 1));
				byteIndex += bytes;
				while (byteIndexToIndex.Count < byteIndex)
					byteIndexToIndex.Add(i);
			}
		}
	}
}
