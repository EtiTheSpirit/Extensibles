using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Shared {

	/// <summary>
	/// Signatures of members common across all binders.
	/// </summary>
	public sealed class SharedBinderSignatures {

		public ReusableSignatureContainer Common { get; }


		internal SharedBinderSignatures(ReusableSignatureContainer common) {
			Common = common;
			MirrorGenerator mirrorGenerator = common.MirrorGenerator;
			
		}

	}
}
