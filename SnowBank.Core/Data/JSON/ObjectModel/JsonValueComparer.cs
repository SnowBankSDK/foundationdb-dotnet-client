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

namespace SnowBank.Data.Json
{

	/// <summary>Comparer for <see cref="JsonValue"/> instances</summary>
	/// <remarks>This comparer uses the "equivalent values" semantic</remarks>
	public static class JsonValueComparer
	{

		/// <summary>Comparer that checks if two <see cref="JsonValue"/> instances are <i>equivalent</i>, meaning that they would both deserialize to the same or equivalent value</summary>
		/// <remarks>For example, the JSON string <c>"123"</c> is equivalent to the JSON number <c>123</c> since both would return "123" when converted to string, or 123 when converted to Int32.</remarks>
		public static readonly JsonValueDefaultComparer Default = new();

		/// <summary>Comparer that checks if two <see cref="JsonValue"/> instances are <i>equivalent</i>, meaning that they would both deserialize to the same or equivalent value</summary>
		/// <remarks>For example, the JSON string <c>"123"</c> is equivalent to the JSON number <c>123</c> since both would return "123" when converted to string, or 123 when converted to Int32.</remarks>
		public static readonly JsonValueStrictComparer Strict = new();

	}

	public sealed class JsonValueDefaultComparer : IEqualityComparer<JsonValue>, IComparer<JsonValue>, System.Collections.IEqualityComparer, System.Collections.IComparer
	{

		public bool Equals(JsonValue? x, JsonValue? y) => ReferenceEquals(x, y) || (x?.Equals(y) ?? y!.IsNull);

		public int GetHashCode(JsonValue? obj) => (obj ?? JsonNull.Null).GetHashCode();

		bool System.Collections.IEqualityComparer.Equals(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return true;
			var jx = (x as JsonValue) ?? JsonValue.FromValue(x);
			var jy = (y as JsonValue) ?? JsonValue.FromValue(y);
			Contract.Debug.Assert(jx is not null && jy is not null);
			return jx.Equals(jy);
		}

		int System.Collections.IEqualityComparer.GetHashCode(object? obj)
		{
			return (obj ?? JsonNull.Null).GetHashCode();
		}

		public int Compare(JsonValue? x, JsonValue? y)
		{
			return ReferenceEquals(x, y) ? 0 : (x ?? JsonNull.Null).CompareTo(y);
		}

		int System.Collections.IComparer.Compare(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return 0;
			var jx = (x as JsonValue) ?? JsonValue.FromValue(x);
			var jy = (y as JsonValue) ?? JsonValue.FromValue(y);
			Contract.Debug.Assert(jx is not null && jy is not null);
			return jx.CompareTo(jy);
		}

	}

	public sealed class JsonValueStrictComparer : IEqualityComparer<JsonValue>, IComparer<JsonValue>, System.Collections.IEqualityComparer, System.Collections.IComparer
	{

		public bool Equals(JsonValue? x, JsonValue? y) => ReferenceEquals(x, y) || (x?.StrictEquals(y) ?? y!.IsNull);

		public int GetHashCode(JsonValue? obj) => (obj ?? JsonNull.Null).GetHashCode();

		bool System.Collections.IEqualityComparer.Equals(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return true;
			var jx = (x as JsonValue) ?? JsonValue.FromValue(x);
			var jy = (y as JsonValue) ?? JsonValue.FromValue(y);
			Contract.Debug.Assert(jx is not null && jy is not null);
			return jx.StrictEquals(jy);
		}

		int System.Collections.IEqualityComparer.GetHashCode(object? obj)
		{
			return (obj ?? JsonNull.Null).GetHashCode();
		}

		public int Compare(JsonValue? x, JsonValue? y)
		{
			return ReferenceEquals(x, y) ? 0 : (x ?? JsonNull.Null).CompareTo(y);
		}

		int System.Collections.IComparer.Compare(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return 0;
			var jx = (x as JsonValue) ?? JsonValue.FromValue(x);
			var jy = (y as JsonValue) ?? JsonValue.FromValue(y);
			Contract.Debug.Assert(jx is not null && jy is not null);
			return jx.CompareTo(jy);
		}

	}

}
