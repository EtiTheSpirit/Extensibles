using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {
	public interface IHasTypeDefOrRef {

		ITypeDefOrRef Reference { get; }

	}
}
