using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Shared {
	public sealed class ReusableSignatureContainer {
		public MirrorGenerator MirrorGenerator { get; }

		public SharedGeneralSignatures General { get; }

		public SharedBinderSignatures Binder { get; }

		public ReusableSignatureContainer(MirrorGenerator mirrorGenerator) {
			MirrorGenerator = mirrorGenerator;
			General = new SharedGeneralSignatures(this);
			Binder = new SharedBinderSignatures(this);
		}

	}
}
