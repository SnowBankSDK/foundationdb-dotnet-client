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

namespace SnowBank.Collections.Caching
{

	public interface ICache<TKey, TElement> : ICollection<KeyValuePair<TKey, TElement>>
	{
		// from ICollection<...>
		// int Count { get; }
		// void Clear();

		/// <summary>Capacité actuelle du cache (en nombre d'items)</summary>
		/// <remarks>Les caches qui n'ont pas de capacité de stockage doivent retourner int.MaxValue.</remarks>
		int Capacity { get; }

		/// <summary>Indique si le cache a une capacité maximale (false), ou s'il n'a pas de limite particulière (false)</summary>
		/// <remarks>Si IsCapped retourne true, il faut consulter la valeur de <see cref="Capacity"/> pour connaître la capacité maximale.</remarks>
		bool IsCapped { get; }

		/// <summary>Comparateur utilisé pour les clés du cache</summary>
		IEqualityComparer<TKey> KeyComparer { get; }

		/// <summary>Retourne un entrée du cache, si elle existe</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="value">Reçoit la valeur en cache si elle existe (et est toujours valide)</param>
		/// <returns>True si la valeur existe dans le cache; false si elle n'existe pas (ou n'est plus valide)</returns>
		bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TElement value);

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="addValue">Valeur qui sera ajoutée dans le cache pour cette clé, si elle n'existait pas</param>
		/// <returns>Valeur de l'entrée si elle existait, ou <paramref name="addValue"/> si elle n'existait pas</returns>
		TElement GetOrAdd(TKey key, TElement addValue);

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="factory">Lambda qui sera appelée pour générée la valeur à ajouter, si elle n'existait pas</param>
		/// <returns>Valeur de l'entrée si elle existait, ou le résultat de <paramref name="factory"/> si elle n'existait pas</returns>
		/// <reremarks>Attention: certains caches n'offrent aucune garantie sur le fait que valueFactory ne soit pas appelé plusieurs fois!</reremarks>
		TElement GetOrAdd(TKey key, [InstantHandle] Func<TKey, TElement> factory);

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="factory">Lambda qui sera appelée pour générée la valeur à ajouter, si elle n'existait pas</param>
		/// <param name="state">Valeur passée en second paramètre à <paramref name="factory"/></param>
		/// <returns>Valeur de l'entrée si elle existait, ou le résultat de <paramref name="factory"/> si elle n'existait pas</returns>
		/// <reremarks>Attention: certains caches n'offrent aucune garantie sur le fait que valueFactory ne soit pas appelé plusieurs fois!</reremarks>
		TElement GetOrAdd<TState>(TKey key, [InstantHandle] Func<TKey, TState, TElement> factory, TState state);

		/// <summary>Ecrase la valeur d'une entrée du cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée</param>
		/// <param name="newValue">Nouvelle valeur</param>
		void SetItem(TKey key, TElement newValue);

		/// <summary>Supprime une entrée du cache</summary>
		/// <param name="key">Clé de l'entrée à supprimer</param>
		/// <returns>True si l'entrée a été supprimée, false si elle n'existait pas</returns>
		bool Remove(TKey key);

		/// <summary>Supprime une entrée du cache, uniquement si elle à une valeur spécifique</summary>
		/// <param name="key">Clé de l'entrée à supprimer</param>
		/// <param name="expectedValue">Valeur que l'entrée doit avoir pour être supprimé</param>
		/// <param name="valueComparer">Comparateur optionnel pour les valeurs</param>
		/// <returns>True si l'entrée existait et avait la valeur attendue, ou false sinon.</returns>
		bool TryRemove(TKey key, TElement expectedValue, IEqualityComparer<TElement>? valueComparer = null);

		/// <summary>Recherche et supprimer des entrées du cache</summary>
		/// <param name="predicate">Prédicat qui retourne true pour les entrées à supprimer</param>
		/// <returns>Nombre d'entrées supprimée dans le cache</returns>
		int Cleanup([InstantHandle] Func<TKey, TElement, bool> predicate);
	}

}
