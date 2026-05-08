#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Networking.PacketCapture
{

	/// <summary>Store that can record a stream of captured packets (to memory, to disk, in a database, ...)</summary>
	public interface IPacketCaptureStore
	{

		/// <summary>Records a captured packet</summary>
		Task AddPacket(CapturedPacket packet, CancellationToken ct);

		/// <summary>Records a batch of captured packets</summary>
		Task AddBatch(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct);

		/// <summary>Returns a previously captured packet, given its identifier</summary>
		Task<CapturedPacketDescriptor?> GetPacket(CapturedPacketId id, CancellationToken ct);
		
		/// <summary>Returns the next batch of previously captured packets after the given cursor</summary>
		Task<CapturedPacketQueryResult> GetNext(CapturedPacketId cursor, /*TODO: options?*/CancellationToken ct);

		/// <summary>Returns the contents of part of a previously captured packet</summary>
		/// <param name="id">Unique id of the packet</param>
		/// <param name="type">The request part (ex: <c>"request"</c> for the Request Body, <c>"response"</c> for the Response Body, ...)</param>
		/// <param name="ct"></param>
		/// <returns>Buffer that contains the requested part, if found</returns>
		Task<Slice> GetBody(string id, string type, CancellationToken ct);

	}

	public sealed record CapturedPacketDescriptor
	{

		/// <summary>Unique identifier of the packet</summary>
		[JsonProperty("id")]
		public required CapturedPacketId Id { get; init; }

		/// <summary>Captured metadata</summary>
		[JsonProperty("metadata")]
		public required CapturedPacketMetadata Metadata { get; init; }

		/// <summary>Id of the blob that contains the Request body (if there is one)</summary>
		[JsonProperty("requestBodyId")]
		public string? RequestBodyId { get; init; }

		/// <summary>Id of the blob that contains the Response body (if there is one)</summary>
		[JsonProperty("responseBodyId")]
		public string? ResponseBodyId { get; init; }

	}

	public sealed record CapturedPacketQueryResult
	{

		[JsonProperty("missingResults")]
		public bool MissingResults { get; set; }

		[JsonProperty("packets")]
		public List<CapturedPacketDescriptor>? Packets { get; set; }

		[JsonProperty("hasMore")]
		public bool HasMore { get; set; }
		
		[JsonProperty("first")]
		public CapturedPacketId First { get; set; }

		[JsonProperty("last")]
		public CapturedPacketId Last { get; set; }

	}

}
