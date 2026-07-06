#if NETSTANDARD2_0
// Polyfill: ChannelReader<T>.ReadAllAsync([CancellationToken]) is available in-box on net8+, but the
// netstandard2.0 build of System.Threading.Channels at our (lowered) floor does not expose it. Provide a
// manual async iterator built on the always-present WaitToReadAsync / TryRead primitives.
namespace System.Threading.Channels
{
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Threading;

	/// <summary>Netstandard2.0 stand-in for <c>ChannelReader&lt;T&gt;.ReadAllAsync</c>.</summary>
	public static class ChannelReaderCompatExtensions
	{

		/// <summary>Asynchronously reads all the data from the channel.</summary>
		public static async IAsyncEnumerable<T> ReadAllAsync<T>(this ChannelReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				while (reader.TryRead(out var item))
				{
					yield return item;
				}
			}
		}

	}

}
#endif
