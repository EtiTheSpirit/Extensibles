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
		public bool mirrorsAreVirtual = true;

		/// <summary>
		/// If true, mirror types will extend the mirror counterpart of whatever the template class extends.
		/// <para/>
		/// To give an example, if the vanilla code has:<br/>
		///	<c>class Foo {} class Bar : Foo {}</c><br/>
		/// Then the same inheritence for mirrors is true:<br/>
		/// <c>class MirrorFoo {} class MirrorBar : MirrorFoo {}</c>.
		/// </summary>
		public bool mirrorTypesInherit = true;

	}
}
