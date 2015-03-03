
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
		/// Value of GL_SCALED_RESOLVE_FASTEST_EXT symbol.
		/// </summary>
		[RequiredByFeature("GL_EXT_framebuffer_multisample_blit_scaled")]
		public const int SCALED_RESOLVE_FASTEST_EXT = 0x90BA;

		/// <summary>
		/// Value of GL_SCALED_RESOLVE_NICEST_EXT symbol.
		/// </summary>
		[RequiredByFeature("GL_EXT_framebuffer_multisample_blit_scaled")]
		public const int SCALED_RESOLVE_NICEST_EXT = 0x90BB;

	}

}