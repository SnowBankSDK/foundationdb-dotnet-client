#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Networking.PacketCapture
{
	public sealed record PacketCaptureStoreOptions
	{
		public const int MAX_ITEMS_COUNT = 1_000;

		public int BufferSize { get; set; } = MAX_ITEMS_COUNT;

	}

}
