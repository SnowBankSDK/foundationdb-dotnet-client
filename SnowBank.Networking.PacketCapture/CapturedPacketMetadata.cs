#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Networking.PacketCapture
{
	using Microsoft.AspNetCore.Http.Headers;

	/// <summary>Holds the metadata about a captured packet.</summary>
	[DebuggerDisplay("Id={TraceId}, Req={Request.Method} {Uri}")]
	public sealed record CapturedPacketMetadata
	{
		public const string ROLE_SERVER = "SERVER";

		public const string ROLE_CLIENT = "CLIENT";

		[JsonProperty("traceId")]
		public required string TraceId { get; init; }

		[JsonProperty("timestamp")]
		public DateTimeOffset? Timestamp { get; init; }

		[JsonProperty("reconstructed")]
		public bool Reconstructed { get; init; }

		[JsonProperty("role")]
		public required string Role { get; init; }

		[JsonProperty("uri")]
		public string? Uri { get; init; }

		[JsonProperty("startedAt")]
		public required Instant StartedAt { get; init; }

		[JsonProperty("processedAt")]
		public Instant? ProcessedAt { get; init; }

		[JsonProperty("endedAt")]
		public Instant? EndedAt { get; init; }

		[JsonProperty("fieldsAt")]
		public required CapturedHttpFields Fields { get; init; }

		[JsonProperty("actorId")]
		public string? ActorId { get; internal set; } // changed by the capture manager

		[JsonProperty("cid")]
		public string? CorrelationId { get; init; }

		[JsonProperty("connection")]
		public required ConnectionInfo Connection { get; init; }

		public sealed record ConnectionInfo
		{
			[JsonProperty("id")]
			public required string Id { get; init; }

			[JsonProperty("startedAt")]
			public Instant StartedAt { get; init; }

			[JsonProperty("remoteHost")]
			public string? RemoteHost { get; init; }

			[JsonProperty("remotePort")]
			public int? RemotePort { get; init; }

			[JsonProperty("localHost")]
			public string? LocalHost { get; init; }

			[JsonProperty("localPort")]
			public int? LocalPort { get; init; }

		}

		[JsonProperty("request")]
		public required RequestInfo Request { get; init; }

		public sealed record RequestInfo
		{
			[JsonProperty("scheme")]
			public string? Scheme { get; init; }

			[JsonProperty("protocol")]
			public string? Protocol { get; init; }

			[JsonProperty("method")]
			public string? Method { get; init; }

			[JsonProperty("path")]
			public string? Path { get; init; }

			[JsonProperty("queryString")]
			public string? QueryString { get; init; }

			[JsonProperty("headers")]
			public required CapturedHttpHeaders Headers { get; init; }

			[JsonProperty("hasBody")]
			public bool HasBody { get; init; }

			public RequestHeaders GetTypedHeaders() => new(this.Headers);

		}

		[JsonProperty("response")]
		public required ResponseInfo Response { get; init; }

		public sealed record ResponseInfo
		{
			[JsonProperty("status")]
			public required int Status { get; init; }

			[JsonProperty("reasonPhrase")]
			public string? ReasonPhrase { get; init; }

			[JsonProperty("headers")]
			public required CapturedHttpHeaders Headers { get; init; }

			[JsonProperty("hasBody")]
			public bool HasBody { get; init; }

			/// <summary>Indicates that the response was a long-lived stream (gRPC duplex or Server-Sent Events) captured at headers only; its body was passed through untouched and deliberately not recorded (so <see cref="HasBody"/> is <see langword="false"/>).</summary>
			[JsonProperty("streaming")]
			public bool Streaming { get; init; }

			public ResponseHeaders GetTypedHeaders() => new(this.Headers);

		}

		[JsonProperty("stackTrace")]
		public string? StackTrace { get; init; }

	}

}
