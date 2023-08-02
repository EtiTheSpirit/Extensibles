using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender {

	/// <summary>
	/// Stores options for the generator.
	/// </summary>
	public sealed class GeneratorSettings {

		/// <summary>
		/// Whether or not to make mirrors virtual.
		/// </summary>
		public bool MirrorsAreVirtual { get; } = true;

		/// <summary>
		/// If true, mirror types will extend the mirror counterpart of whatever the template class extends.
		/// <para/>
		/// To give an example, if the vanilla code has:<br/>
		///	<c>class Foo {} class Bar : Foo {}</c><br/>
		/// Then the same inheritence for mirrors is true:<br/>
		/// <c>class MirrorFoo {} class MirrorBar : MirrorFoo {}</c>.
		/// </summary>
		public bool MirrorTypesInherit { get; } = true;

		public GeneratorSettings(bool mirrorsAreVirtual = true, bool mirrorTypesInherit = true) { 
			MirrorsAreVirtual = mirrorsAreVirtual;
			MirrorTypesInherit = mirrorTypesInherit;
		}

	}
}
