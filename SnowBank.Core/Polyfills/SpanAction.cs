#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

// System.Buffers.SpanAction<T, TArg> is not part of the System.Memory facade on netstandard2.0.
// It only exists in the netstandard2.0 build; modern targets use the real BCL delegate.

#if NETSTANDARD2_0

namespace System.Buffers
{

	/// <summary>Encapsulates a method that receives a span of objects of type <typeparamref name="T"/> and a state object of type <typeparamref name="TArg"/>.</summary>
	public delegate void SpanAction<T, in TArg>(Span<T> span, TArg arg);

}

#endif
