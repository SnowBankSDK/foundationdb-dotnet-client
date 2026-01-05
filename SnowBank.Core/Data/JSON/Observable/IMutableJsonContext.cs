#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Data.Json
{

	/// <summary>Context that will record all the changes made to a <see cref="MutableJsonValue"/></summary>
	[PublicAPI]
	public interface IMutableJsonContext
	{

		/// <summary>Tests if the underlying observed object has been changed during this transaction</summary>
		/// <remarks>Returns <c>false</c> if no changes have been made, or if alls changes did not actually mutate the value.</remarks>
		bool HasMutations { get; }

		/// <summary>Number of changes registered on this transaction</summary>
		int Count { get; }

		/// <summary>Reset the transaction to its initial state, reverting any previous mutations.</summary>
		/// <remarks>This method can be used to reuse the current transaction instance</remarks>
		void Reset();

		/// <summary>Wraps a top-level <see cref="JsonValue"/> into a <see cref="MutableJsonValue"/> that will be monitored by this context</summary>
		/// <param name="value">JSON value that represents the top-most object or array of the document</param>
		/// <remarks>Mutable value that will notify this context whenever it (or any of its descendant) is mutated</remarks>
		MutableJsonValue FromJson(JsonValue value);

		/// <summary>Wraps a <see cref="JsonValue"/> into a <see cref="MutableJsonValue"/> that will be monitored by this context</summary>
		/// <param name="parent">Parent that contains this value</param>
		/// <param name="path">Name or index of this value in its parent</param>
		/// <param name="value">JSON value that must be wrapped</param>
		/// <remarks>Mutable value that will notify this context whenever it (or any of its descendant) is mutated</remarks>
		MutableJsonValue FromJson(MutableJsonValue parent, JsonPathSegment path, JsonValue? value);

		/// <summary>Creates a new empty object, using the transactions default settings</summary>
		JsonObject NewObject();

		/// <summary>Creates a new empty array, using the transactions default settings</summary>
		JsonArray NewArray();

		/// <summary>Records the insertion of an item in an array</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="offset">Index where to insert the child, or <c>^0</c> to append at the end of the array.</param>
		/// <param name="argument">Value of the new child</param>
		/// <remarks>
		/// <para>This records the facts that a new item is inserted to an array.</para>
		/// </remarks>
		void RecordInsertAt(IJsonProxyNode instance, Index offset, JsonValue argument);

		/// <summary>Truncate an array to the specified length</summary>
		/// <param name="instance">Parent instance (expected to be an array)</param>
		/// <param name="length">New length of the array</param>
		/// <remarks>
		/// <para>Any item that would fall outside the new bounds of the array will be removed.</para>
		/// <para>If the array is smaller than <paramref name="length"/>, extra <c>null</c> entries will be appended as needed.</para>
		/// </remarks>
		void RecordTruncate(IJsonProxyNode instance, int length);

		/// <summary>Records the update of a field of an object or item of an array, with a new explicit value</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="child">Path of the child (name of field for objects, index of item for arrays), or <see cref="JsonPathSegment.Empty"/> for the document itself</param>
		/// <param name="argument">Updated value of the field</param>
		/// <remarks>
		/// <para>This records the fact that the value of a field of the object as been replaced by another value.</para>
		/// <para>Any previous mutation on this field, or any child, is superseded by this record</para>
		/// </remarks>
		void RecordUpdate(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument);

		/// <summary>Records the update of an existing field of an object or item of an array, using a patch definition</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="child">Path of the child (name of field for objects, index of item for arrays), or <see cref="JsonPathSegment.Empty"/> for the document itself</param>
		/// <param name="argument">Patch that describes the changes to this field</param>
		/// <remarks>
		/// <para>This records the fact that the value of a field of the object as been patched.</para>
		/// <para>Any previous mutation on this field, or any child, should be merged with this patch</para>
		/// </remarks>
		void RecordPatch(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument);

		/// <summary>Records the deletion of an existing field of an object or item of an array</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="child">Path of the child (name of field for objects, index of item for arrays)</param>
		/// <remarks>
		/// <para>This records the fact that the field does not exist any longer in the object</para>
		/// <para>Setting a field to null is logically equivalent to deleting the field</para>
		/// <para>Any previous mutation on this item, or any child, should be superseded by this record.</para>
		/// </remarks>
		void RecordDelete(IJsonProxyNode instance, JsonPathSegment child);

	}

}
