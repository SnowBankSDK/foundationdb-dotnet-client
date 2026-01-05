#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client.Testing
{

	[DebuggerDisplay("Count={Count}")]
	[DebuggerTypeProxy(typeof(ContextStack<>.DebugView))]
	internal sealed class ContextStack<T>
	{

		private T[] Items = [];

		public int Count { get; private set; }

		private sealed class DebugView
		{

			public DebugView(ContextStack<T> stack)
			{
				var items = stack.ToArray();
				Array.Reverse(items);
				this.Items = items;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public T[] Items { get; }

		}

		private ref T GetAt(int index) => ref this.Items[this.Count - index - 1];

		public T this[int index] => this.Count > 0 ? GetAt(index) : throw new IndexOutOfRangeException();

		public void Clear()
		{
			this.Items.AsSpan(0, this.Count).Clear();
			this.Count = 0;
		}

		/// <summary>Push the provided item onto the stack</summary>
		public void Push(T item)
		{
			var items = this.Items;
			var count = this.Count;
			if (count >= items.Length)
			{
				Array.Resize(ref items, items.Length + 4);
				this.Items = items;
			}

			items[count] = item;
			this.Count = count + 1;
		}

		/// <summary>Pops and discard the top item on the stack</summary>
		public T Pop()
		{
			var count = this.Count;
			if (count == 0) throw new InvalidOperationException("The stack is empty");

			ref var slot = ref GetAt(0);
			var item = slot;
			slot = default!;
			this.Count = count - 1;
			return item;
		}

		/// <summary>Try to pop and discard the top item on the stack, unless it is empty.</summary>
		public bool TryPop([MaybeNullWhen(false)] out T item)
		{
			var count = this.Count;
			if (count == 0)
			{
				item = default;
				return false;
			}

			ref var slot = ref GetAt(0);
			item = slot;
			slot = default!;
			this.Count = count - 1;
			return true;
		}

		/// <summary>Return the top item on the stack, without discarding it.</summary>
		public T Peek()
		{
			return this.Count != 0 ? GetAt(0) : throw new InvalidOperationException("The stack is empty");
		}

		/// <summary>Return the top item on the stack, without discarding it, unless it is empty.</summary>
		public bool TryPeek([MaybeNullWhen(false)] out T item)
		{
			var count = this.Count;
			if (count == 0)
			{
				item = default;
				return false;
			}
			item = GetAt(0);
			return true;
		}

		/// <summary>Duplicate the top item on the stack</summary>
		public void Dup()
		{
			Push(Peek());
		}

		/// <summary>Swap the items at the specified locations</summary>
		public void Swap(int a, int b)
		{
			var count = this.Count;
			if ((uint) a > count) throw new ArgumentOutOfRangeException(nameof(a), a, "Outside the bounds of the stack");
			if ((uint) b > count) throw new ArgumentOutOfRangeException(nameof(b), b, "Outside the bounds of the stack");

			if (a != b)
			{
				ref var sa = ref GetAt(a);
				ref var sb = ref GetAt(b);
				(sa, sb) = (sb, sa);
			}
		}

		/// <summary>Return the content of the stack, from oldest to newest</summary>
		public T[] ToArray()
		{
			return this.Items.AsSpan(0, this.Count).ToArray();
		}

	}

}
