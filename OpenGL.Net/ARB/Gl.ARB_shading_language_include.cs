
// Copyright (C) 2015 Luca Piccioni
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
// USA

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenGL
{
	public partial class Gl
	{
		/// <summary>
		/// Value of GL_SHADER_INCLUDE_ARB symbol.
		/// </summary>
		[RequiredByFeature("GL_ARB_shading_language_include")]
		public const int SHADER_INCLUDE_ARB = 0x8DAE;

		/// <summary>
		/// Value of GL_NAMED_STRING_LENGTH_ARB symbol.
		/// </summary>
		[RequiredByFeature("GL_ARB_shading_language_include")]
		public const int NAMED_STRING_LENGTH_ARB = 0x8DE9;

		/// <summary>
		/// Value of GL_NAMED_STRING_TYPE_ARB symbol.
		/// </summary>
		[RequiredByFeature("GL_ARB_shading_language_include")]
		public const int NAMED_STRING_TYPE_ARB = 0x8DEA;

		/// <summary>
		/// Binding for glNamedStringARB.
		/// </summary>
		/// <param name="type">
		/// A <see cref="T:int"/>.
		/// </param>
		/// <param name="namelen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="name">
		/// A <see cref="T:String"/>.
		/// </param>
		/// <param name="stringlen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="string">
		/// A <see cref="T:String"/>.
		/// </param>
		public static void NamedStringARB(int type, Int32 namelen, String name, Int32 stringlen, String @string)
		{
			Debug.Assert(Delegates.pglNamedStringARB != null, "pglNamedStringARB not implemented");
			Delegates.pglNamedStringARB(type, namelen, name, stringlen, @string);
			CallLog("glNamedStringARB({0}, {1}, {2}, {3}, {4})", type, namelen, name, stringlen, @string);
			DebugCheckErrors();
		}

		/// <summary>
		/// Binding for glDeleteNamedStringARB.
		/// </summary>
		/// <param name="namelen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="name">
		/// A <see cref="T:String"/>.
		/// </param>
		public static void DeleteNamedStringARB(Int32 namelen, String name)
		{
			Debug.Assert(Delegates.pglDeleteNamedStringARB != null, "pglDeleteNamedStringARB not implemented");
			Delegates.pglDeleteNamedStringARB(namelen, name);
			CallLog("glDeleteNamedStringARB({0}, {1})", namelen, name);
			DebugCheckErrors();
		}

		/// <summary>
		/// Binding for glCompileShaderIncludeARB.
		/// </summary>
		/// <param name="shader">
		/// A <see cref="T:UInt32"/>.
		/// </param>
		/// <param name="count">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="path">
		/// A <see cref="T:String[]"/>.
		/// </param>
		/// <param name="length">
		/// A <see cref="T:Int32[]"/>.
		/// </param>
		public static void CompileShaderIncludeARB(UInt32 shader, Int32 count, String[] path, Int32[] length)
		{
			unsafe {
				fixed (Int32* p_length = length)
				{
					Debug.Assert(Delegates.pglCompileShaderIncludeARB != null, "pglCompileShaderIncludeARB not implemented");
					Delegates.pglCompileShaderIncludeARB(shader, count, path, p_length);
					CallLog("glCompileShaderIncludeARB({0}, {1}, {2}, {3})", shader, count, path, length);
				}
			}
			DebugCheckErrors();
		}

		/// <summary>
		/// Binding for glIsNamedStringARB.
		/// </summary>
		/// <param name="namelen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="name">
		/// A <see cref="T:String"/>.
		/// </param>
		public static bool IsNamedStringARB(Int32 namelen, String name)
		{
			bool retValue;

			Debug.Assert(Delegates.pglIsNamedStringARB != null, "pglIsNamedStringARB not implemented");
			retValue = Delegates.pglIsNamedStringARB(namelen, name);
			CallLog("glIsNamedStringARB({0}, {1}) = {2}", namelen, name, retValue);
			DebugCheckErrors();

			return (retValue);
		}

		/// <summary>
		/// Binding for glGetNamedStringARB.
		/// </summary>
		/// <param name="namelen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="name">
		/// A <see cref="T:String"/>.
		/// </param>
		/// <param name="bufSize">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="stringlen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="string">
		/// A <see cref="T:StringBuilder"/>.
		/// </param>
		public static void GetNamedStringARB(Int32 namelen, String name, Int32 bufSize, out Int32 stringlen, [Out] StringBuilder @string)
		{
			unsafe {
				fixed (Int32* p_stringlen = &stringlen)
				{
					Debug.Assert(Delegates.pglGetNamedStringARB != null, "pglGetNamedStringARB not implemented");
					Delegates.pglGetNamedStringARB(namelen, name, bufSize, p_stringlen, @string);
					CallLog("glGetNamedStringARB({0}, {1}, {2}, {3}, {4})", namelen, name, bufSize, stringlen, @string);
				}
			}
			DebugCheckErrors();
		}

		/// <summary>
		/// Binding for glGetNamedStringivARB.
		/// </summary>
		/// <param name="namelen">
		/// A <see cref="T:Int32"/>.
		/// </param>
		/// <param name="name">
		/// A <see cref="T:String"/>.
		/// </param>
		/// <param name="pname">
		/// A <see cref="T:int"/>.
		/// </param>
		/// <param name="params">
		/// A <see cref="T:Int32[]"/>.
		/// </param>
		public static void GetNamedStringARB(Int32 namelen, String name, int pname, Int32[] @params)
		{
			unsafe {
				fixed (Int32* p_params = @params)
				{
					Debug.Assert(Delegates.pglGetNamedStringivARB != null, "pglGetNamedStringivARB not implemented");
					Delegates.pglGetNamedStringivARB(namelen, name, pname, p_params);
					CallLog("glGetNamedStringivARB({0}, {1}, {2}, {3})", namelen, name, pname, @params);
				}
			}
			DebugCheckErrors();
		}

	}

}