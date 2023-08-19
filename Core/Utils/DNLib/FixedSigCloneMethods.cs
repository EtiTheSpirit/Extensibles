using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CallingConvention = dnlib.DotNet.CallingConvention;

namespace HookGenExtender.Core.Utils.DNLib {
	public static class FixedSigCloneMethods {

		private static ConstructorInfo _propSigCloneCtor = null;
		private static object[] _propSigCloneCtorParams = new object[5];

		/// <summary>
		/// Clones a method sig, such that the parameter lists are not shared references.
		/// </summary>
		/// <param name="original"></param>
		/// <returns></returns>
		public static MethodSig DeepClone(this MethodSig original) => new MethodSig(original.CallingConvention, original.GenParamCount, original.RetType, new List<TypeSig>(original.Params), original.ParamsAfterSentinel != null ? new List<TypeSig>(original.ParamsAfterSentinel) : null);
		
		/// <summary>
		/// Clones a property sig, such that the parameter lists are not shared references.
		/// </summary>
		/// <param name="original"></param>
		/// <returns></returns>
		public static PropertySig DeepClone(this PropertySig original) {
			if (_propSigCloneCtor == null) {
				_propSigCloneCtor = typeof(PropertySig).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(CallingConvention), typeof(uint), typeof(TypeSig), typeof(IList<TypeSig>), typeof(IList<TypeSig>) }, null);
			}
			_propSigCloneCtorParams[0] = original.CallingConvention;
			_propSigCloneCtorParams[1] = original.GenParamCount;
			_propSigCloneCtorParams[2] = original.RetType;
			_propSigCloneCtorParams[3] = new List<TypeSig>(original.Params);
			_propSigCloneCtorParams[4] = original.ParamsAfterSentinel != null ? new List<TypeSig>(original.ParamsAfterSentinel) : null;
			return (PropertySig) _propSigCloneCtor.Invoke(_propSigCloneCtorParams);
		}
	}
}
