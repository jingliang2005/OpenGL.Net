
// Copyright (C) 2009-2019 Luca Piccioni
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenGL.Objects
{
	/// <summary>
	/// Shader program.
	/// </summary>
	[DebuggerDisplay("ShaderProgram: Name={ObjectName} Id={Identifier} Linked={IsLinked}")]
	public partial class ShaderProgram : GraphicsResource, IBindingResource, IShaderUniformContainer
	{
		#region Constructors

		/// <summary>
		/// Construct a ShaderProgram.
		/// </summary>
		/// <param name="programName">
		/// A <see cref="String"/> that specify the shader program name.
		/// </param>
		/// <exception cref="ArgumentException">
		/// This exception is thrown if the parameter <paramref name="programName"/> is not a valid name.
		/// </exception>
		public ShaderProgram(string programName) : base(programName)
		{
			
		}

		#endregion

		#region Program Linkage

		/// <summary>
		/// Create this ShaderProgram, specifying the compiler parameters.
		/// </summary>
		/// <param name="cctx">
		/// A <see cref="ShaderCompilerContext"/> that specify the compiler parameters used for compiling and
		/// linking this ShaderProgram.
		/// </param>
		public void Create(ShaderCompilerContext cctx)
		{
			// Linkage status is reset is compilation parameters changes
			IsLinked = CompilationParams != cctx;
			// Cache compilation parameters (used by CreateObject)
			CompilationParams = cctx;
		}

		/// <summary>
		/// Create this ShaderProgram, specifying the compiler parameters.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for creating this object.
		/// </param>
		/// <param name="cctx">
		/// A <see cref="ShaderCompilerContext"/> that specify the compiler parameters used for compiling and
		/// linking this ShaderProgram.
		/// </param>
		public void Create(GraphicsContext ctx, ShaderCompilerContext cctx)
		{
			CheckCurrentContext(ctx);
			if (cctx == null)
				throw new ArgumentNullException("cctx");

			// Cache compilation parameters (used by CreateObject)
			_CompilationParams = cctx;
			// Base implementation
			base.Create(ctx);
		}

		/// <summary>
		/// ShaderCompilerContext used for linkage. This property will not ever be null. If users set this property
		/// to null, it reset to the default one.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Exception thrown if the property setter is executed after the program has been created (i.e. linked).
		/// </exception>
		public ShaderCompilerContext CompilationParams
		{
			get { return _CompilationParams; }
			set
			{
				if (value != _CompilationParams) {
					_CompilationParams = value ?? new ShaderCompilerContext();
					IsLinked = false;
				}
			}
		}

		/// <summary>
		/// ShaderCompilerContext used for compilation.
		/// </summary>
		private ShaderCompilerContext _CompilationParams;

		/// <summary>
		/// Attach a Shader to this ShaderProgram.
		/// </summary>
		/// <param name="shaderObject">
		/// A <see cref="Shader"/> to be attached to this ShaderProgram.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="shaderObject"/> is null.
		/// </exception>
		public void Attach(Shader shaderObject)
		{
			if (shaderObject == null)
				throw new ArgumentNullException(nameof(shaderObject));
			if (_ProgramObjects.Contains(shaderObject))
				throw new ArgumentException("already attached", nameof(shaderObject));

			// Link object
			_ProgramObjects.Add(shaderObject);
			// Force relink
			IsLinked = false;
		}

		/// <summary>
		/// Utility routines for detaching all shaders from this ShaderProgram.
		/// </summary>
		private void DetachShaders(GraphicsContext ctx)
		{
			CheckThisExistence(ctx);

			// Get the currently attached shaders (avoid to re-attach same shaders)
			int shadersCount;

			Gl.GetProgram(ObjectName, ProgramProperty.AttachedShaders, out shadersCount);

			uint[] shadersObject = new uint[shadersCount];

			if (shadersCount > 0) {
				Gl.GetAttachedShaders(ObjectName, out shadersCount, shadersObject);
				Debug.Assert(shadersCount == shadersObject.Length);
			}

			foreach (uint shaderObjectName in shadersObject)
				Gl.DetachShader(ObjectName, shaderObjectName);
		}

		/// <summary>
		/// List of shader objects composing this shader program.
		/// </summary>
		private readonly ResourceCollection<Shader> _ProgramObjects = new ResourceCollection<Shader>();

		/// <summary>
		/// Link this ShaderProgram.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for linking this ShaderProgram.
		/// </param>
		/// <param name="cctx">
		/// A <see cref="ShaderCompilerContext"/> that specify additional compiler parameters.
		/// </param>
		/// <remarks>
		/// <para>
		/// Generate shader program source code, compile and link it. After a successfull
		/// link, obtain every information about active uniform and input variables.
		/// </para>
		/// </remarks>
		/// <exception cref="ShaderException">
		/// Exception throw in the case this ShaderProgram is not linkable.
		/// </exception>
		private void Link(GraphicsContext ctx)
		{
			CheckCurrentContext(ctx);

			if (_CompilationParams == null)
				throw new InvalidOperationException("no compilation parameters");
			
			#region Compile and Attach Shader Objects

			// Ensure no shader is attached
#if DEBUG
			int shadersCount;

			Gl.GetProgram(ObjectName, ProgramProperty.AttachedShaders, out shadersCount);
			Debug.Assert(shadersCount == 0, "shaders attached, while they shouldn't");
#endif

			// Attach shaders
			foreach (Shader shader in _ProgramObjects) {
				// Re-compile shader, if necessary
				if (!shader.IsCompiled)
					shader.Create(ctx, _CompilationParams);

				Gl.AttachShader(ObjectName, shader.ObjectName);
			}

			#endregion

			#region Transform Feedback Definition

			IntPtr[] feedbackVaryingsPtrs = null;

			if ((_FeedbackVaryings != null) && (_FeedbackVaryings.Count > 0)) {
				Log("Feedback varyings ({0}):", _CompilationParams.FeedbackVaryingsFormat);
				foreach (string feedbackVarying in _FeedbackVaryings)
					Log("- {0}", feedbackVarying);

				if (ctx.Extensions.TransformFeedback2_ARB || ctx.Extensions.TransformFeedback_EXT) {
					string[] feedbackVaryings = _FeedbackVaryings.ToArray();

					// Bug in NVIDIA drivers? Not exactly, but the NVIDIA driver hold the 'feedbackVaryings' pointer until
					// glLinkProgram is executed, causing linker errors like 'duplicate varying names are not allowed' or garbaging
					// part of the returned strings via glGetTransformFeedbackVarying
					feedbackVaryingsPtrs = feedbackVaryings.AllocHGlobal();

					// Specify feedback varyings
					Gl.TransformFeedbackVaryings(ObjectName, feedbackVaryingsPtrs, (int)_CompilationParams.FeedbackVaryingsFormat);
				} else if (ctx.Extensions.TransformFeedback2_NV) {
					// Nothing to do ATM
				} else
					throw new InvalidOperationException("transform feedback not supported");
			}

			#endregion

			#region Bind Attribute Locations

			if (ctx.Extensions.ExplicitAttribLocation_ARB == false) {
				foreach (KeyValuePair<string, AttributeMetadata> pair in _AttributeMetadata) {
					if (pair.Value.Location < 0)
						continue;

					Gl.BindAttribLocation(ObjectName, (uint)pair.Value.Location, pair.Key);
				}
			}

			#endregion

			#region Bind Fragment Locations

			if (ctx.Extensions.GpuShader4_EXT) {
				// Setup fragment locations, where defined
				foreach (KeyValuePair<string, int> pair in _FragLocations) {
					if (pair.Value >= 0)
						Gl.BindFragDataLocation(ObjectName, (uint) pair.Value, pair.Key);
				}
			}

			#endregion

			#region Separable Program

			if (ctx.Extensions.SeparateShaderObjects_ARB)
				_UniformBackend = new UniformBackendSeparate();
			else
				_UniformBackend = new UniformBackendCompatible();		// Defaults to compatible

			#endregion

			#region Link Shader Program Objects

			int linkStatus;

			Log("=== Link shader program {0}", Identifier ?? "<Unnamed>");

			// Link shader program
			Gl.LinkProgram(ObjectName);
			// Check for linking errors
			Gl.GetProgram(ObjectName, ProgramProperty.LinkStatus, out linkStatus);

			// Ensure shaders can be deleted hereafter
			DetachShaders(ctx);

			// Release feedback varyings unmanaged memory
			if (feedbackVaryingsPtrs != null)
				feedbackVaryingsPtrs.FreeHGlobal();

			if (linkStatus != Gl.TRUE) {
				const int MaxInfoLength = 4096;

				StringBuilder logInfo = new StringBuilder(MaxInfoLength);
				int logLength;

				// Obtain compilation log
				Gl.GetProgramInfoLog(ObjectName, MaxInfoLength, out logLength, logInfo);

				// Stop link process
				StringBuilder sb = new StringBuilder(logInfo.Capacity);

				string[] compilerLogLines = logInfo.ToString().Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
				foreach (string logLine in compilerLogLines)
					sb.AppendLine("  $ " + logLine);

				Log("Shader program \"{0}\" linkage failed: {1}", Identifier ?? "<Unnamed>", sb.ToString());

				throw new ShaderException("shader program is not valid. Linker output for {0}: {1}\n", Identifier ?? "<Unnamed>", sb.ToString());
			}
			
			// From now we're linked
			IsLinked = true;

			#endregion

			CollectionLinkedInformation(ctx, _CompilationParams);
		}

		/// <summary>
		/// Introspect all information about this ShaderProgram.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for linking this ShaderProgram.
		/// </param>
		/// <param name="cctx">
		/// A <see cref="ShaderCompilerContext"/> that specify additional compiler parameters.
		/// </param>
		private void CollectionLinkedInformation(GraphicsContext ctx, ShaderCompilerContext cctx)
		{
			CollectActiveUniforms(ctx);
			CollectActiveUniformBlocks(ctx);
			CollectActiveStorageBuffers(ctx);

			#region Collect Active Program Inputs

			// Get active inputs count
			int activeInputs, attributeBufferSize;

			Gl.GetProgram(ObjectName, ProgramProperty.ActiveAttributes, out activeInputs);
			// Get inputs maximum length for name
			Gl.GetProgram(ObjectName, ProgramProperty.ActiveAttributeMaxLength, out attributeBufferSize);

			// Clear input mapping
			_AttributesMap.Clear();

			// Collect input location mapping
			for (uint i = 0; i < (uint)activeInputs; i++) {
				StringBuilder nameBuffer = new StringBuilder(attributeBufferSize);
				int nameLength, size, type;

				// Mono optimize StringBuilder capacity after P/Invoke... ensure enought room for the current loop
				nameBuffer.EnsureCapacity(attributeBufferSize);

				// Obtain active input informations
				Gl.GetActiveAttrib(ObjectName, i, attributeBufferSize, out nameLength, out size, out type, nameBuffer);
				// Obtain active input location
				string name = nameBuffer.ToString();

				int location = Gl.GetAttribLocation(ObjectName, name);
				// Map active input
				_AttributesMap[name] = new AttributeBinding((uint)location, (ShaderAttributeType)type);
			}

			if (LogEnabled) {
				// Log attribute mapping
				List<KeyValuePair<string, AttributeBinding>> attributeNames = new List<KeyValuePair<string, AttributeBinding>>(_AttributesMap);

				// Make attribute list invariant respect the used driver (ease log comparation)
				attributeNames.Sort(delegate(KeyValuePair<string, AttributeBinding> x, KeyValuePair<string, AttributeBinding> y) {
					return (x.Value.Location.CompareTo(y.Value.Location));
				});
			
				Log("Shader program active attributes:");
				foreach (KeyValuePair<string, AttributeBinding> pair in attributeNames)
					Log("\tAttribute {0} (Type: {1}, Location: {2})", pair.Key, pair.Value.Type, pair.Value.Location);
			}

			#endregion

			#region Collect Fragment Locations

			if (ctx.Extensions.GpuShader4_EXT) {
				// Get fragment locations, just in the case automatically assigned
				foreach (string fragOutputName in new List<string>(_FragLocations.Keys))
					_FragLocations[fragOutputName] = Gl.GetFragDataLocation(ObjectName, fragOutputName);
			}

			#endregion
			
			#region Collect Feedback Varyings
			
			if ((_FeedbackVaryings != null) && (_FeedbackVaryings.Count > 0)) {
				if (ctx.Extensions.TransformFeedback2_ARB || ctx.Extensions.TransformFeedback_EXT) {
					// Map active feedback
					int feebackVaryings, feebackVaryingsMaxLength;

					Gl.GetProgram(ObjectName, ProgramProperty.TransformFeedbackVaryings, out feebackVaryings);
					Gl.GetProgram(ObjectName, ProgramProperty.TransformFeedbackVaryingMaxLength, out feebackVaryingsMaxLength);

					for (uint i = 0; i < feebackVaryings; i++) {
						StringBuilder sb = new StringBuilder(feebackVaryingsMaxLength);
						int length = 0, size = 0, type = 0;

						Gl.GetTransformFeedbackVarying(ObjectName, (uint)i, feebackVaryingsMaxLength, out length, out size, out type, sb);
						_FeedbacksMap.Add(sb.ToString(), new FeedbackBinding((ShaderAttributeType)type, (uint)size));
					}
#if !MONODROID
				} else if (Gl.CurrentExtensions.TransformFeedback2_NV && cctx != null) {
					// Activate varyings
					foreach (string feedbackVaryingName in _FeedbackVaryings) {
						Gl.ActiveVaryingNV(ObjectName, feedbackVaryingName);
					}
					
					// Map active feedback
					int feebackVaryings, feebackVaryingsMaxLength;

					Gl.GetProgram(ObjectName, (ProgramProperty)Gl.ACTIVE_VARYINGS_NV, out feebackVaryings);
					Gl.GetProgram(ObjectName, (ProgramProperty)Gl.ACTIVE_VARYING_MAX_LENGTH_NV, out feebackVaryingsMaxLength);

					for (uint i = 0; i < feebackVaryings; i++) {
						StringBuilder sb = new StringBuilder(feebackVaryingsMaxLength * 2);
						int length = 0, size = 0, type = 0;

						Gl.GetActiveVaryingNV(ObjectName, (uint)i, feebackVaryingsMaxLength * 2, out length, out size, out type, sb);

						_FeedbacksMap.Add(sb.ToString(), new FeedbackBinding((ShaderAttributeType)type, (uint)size));
					}

					// Specify feedback varyings
					List<int> feedbackLocations = new List<int>();

					foreach (string feedbackVaryingName in _FeedbackVaryings) {
						int location = Gl.GetVaryingLocationNV(ObjectName, feedbackVaryingName);

						if (location >= 0)
							feedbackLocations.Add(location);
					}

					Gl.TransformFeedbackVaryingsNV(ObjectName, feedbackLocations.ToArray(), cctx.FeedbackVaryingsFormat);

					// Map active feedback

#endif
				}

				Debug.Assert(_FeedbacksMap.Count > 0);

				// Log feedback mapping
				Log("Shader program active feedbacks:");
				foreach (string feedbackName in _FeedbacksMap.Keys)
					Log("\tFeedback {0} (Type: {1})", feedbackName, _FeedbacksMap[feedbackName].Type);
			}

			#endregion
		}

		/// <summary>
		/// Get the number of slots occupied by an uniform type.
		/// </summary>
		/// <param name="uniformType"></param>
		/// <returns></returns>
		private static uint GetUniformSlotCount(ShaderUniformType uniformType)
		{
			switch (uniformType) {
				case ShaderUniformType.Float:
				case ShaderUniformType.Int:
				case ShaderUniformType.UInt:
				case ShaderUniformType.Bool:
					return (1);
				case ShaderUniformType.Vec2:
				case ShaderUniformType.IntVec2:
				case ShaderUniformType.UIntVec2:
				case ShaderUniformType.BoolVec2:
					return (2);
				case ShaderUniformType.Vec3:
				case ShaderUniformType.IntVec3:
				case ShaderUniformType.UIntVec3:
				case ShaderUniformType.BoolVec3:
					return (3);
				case ShaderUniformType.Vec4:
				case ShaderUniformType.IntVec4:
				case ShaderUniformType.UIntVec4:
				case ShaderUniformType.BoolVec4:
					return (4);
				case ShaderUniformType.Mat2x2:
					return (4);
				case ShaderUniformType.Mat3x3:
					return (9);
				case ShaderUniformType.Mat4x4:
					return (16);
				case ShaderUniformType.Mat2x3:
				case ShaderUniformType.Mat3x2:
					return (6);
				case ShaderUniformType.Mat2x4:
				case ShaderUniformType.Mat4x2:
					return (8);
				case ShaderUniformType.Mat3x4:
				case ShaderUniformType.Mat4x3:
					return (12);
				default:
					// Assume sampler type
					return (1);
				case ShaderUniformType.Unknown:
					throw new ArgumentException("invalid type", "uniformType");
			}
		}

		/// <summary>
		/// Property to determine program linkage status.
		/// </summary>
		public bool IsLinked { get; private set; }

		/// <summary>
		/// Validate this shader program.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Throw an Exception in the case the validation has failed.
		/// </exception>
		[Conditional("DEBUG")]
		private void Validate()
		{
			int validationStatus;

			if (IsLinked == false)
				throw new InvalidOperationException("not linked");

			// Request program validation
			Gl.ValidateProgram(ObjectName);
			// Check for validation result
			Gl.GetProgram(ObjectName, ProgramProperty.ValidateStatus, out validationStatus);

			if (validationStatus != Gl.TRUE) {
				const int MaxInfoLength = 4096;

				StringBuilder logInfo = new StringBuilder(256, MaxInfoLength);
				int logLength;

				// Obtain compilation log
				Gl.GetProgramInfoLog(ObjectName, MaxInfoLength, out logLength, logInfo);

				// Stop link process
				StringBuilder sb = new StringBuilder(logInfo.Capacity);

				sb.AppendLine("shader program is not valid. Linker output:");
				string[] linkerLogLines = logInfo.ToString().Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
				foreach (string logLine in linkerLogLines)
					sb.AppendLine("  > " + logLine);

				throw new InvalidOperationException(sb.ToString());
			}
		}

		#endregion

		#region Program Loading

		/// <summary>
		/// Create this ShaderProgram, specifying the program binary.
		/// </summary>
		/// <param name="programBinary">
		/// 
		/// </param>
		public void Create(byte[] programBinary, int programBinaryFormat)
		{
			if (programBinary == null)
				throw new ArgumentNullException(nameof(programBinary));

			// Linkage status is reset is loading another binary
			IsLinked = false;
			// Cache load parameters (used by CreateObject)
			_ProgramBinary = programBinary;
			_ProgramBinaryFormat = programBinaryFormat;
		}

		/// <summary>
		/// Create this ShaderProgram, specifying the program binary.
		/// </summary>
		/// <param name="programBinary">
		/// 
		/// </param>
		public void Create(GraphicsContext ctx, byte[] programBinary, int programBinaryFormat)
		{
			Create(programBinary, programBinaryFormat);
			Create(ctx);
		}

		private byte[] _ProgramBinary;

		private int _ProgramBinaryFormat;

		/// <summary>
		/// Load this ShaderProgram.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for linking this ShaderProgram.
		/// </param>
		private void Load(GraphicsContext ctx)
		{
			CheckCurrentContext(ctx);

			#region Load Binary Format

			// This call is equivalent to a link operation
			Gl.ProgramBinary(ObjectName, _ProgramBinaryFormat, _ProgramBinary, _ProgramBinary.Length);
			// From now we're linked
			IsLinked = true;

			// No reason to keep a reference of binary
			_ProgramBinary = null;
			_ProgramBinaryFormat = 0;

			#endregion

			CollectionLinkedInformation(ctx, null);
		}

		#endregion

		#region Program Attributes

		/// <summary>
		/// Collection of active attributes on this ShaderProgram.
		/// </summary>
		public ICollection<string> ActiveAttributes
		{
			get {
				return (_AttributesMap.Keys);
			}
		}

		/// <summary>
		/// Determine whether an attributes is active or not.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> which specify the input name.
		/// </param>
		/// <returns>
		/// It returns true in the case the input named <paramref name="attributeName"/> is active.
		/// </returns>
		public bool IsActiveAttribute(string attributeName)
		{
			return (_AttributesMap.ContainsKey(attributeName));
		}

		/// <summary>
		/// Active attributes binding information.
		/// </summary>
		/// <param name="attributeName"></param>
		/// <returns></returns>
		internal AttributeBinding GetActiveAttribute(string attributeName)
		{
			return (_AttributesMap[attributeName]);
		}

		/// <summary>
		/// Information about a shader program attribute.
		/// </summary>
		internal class AttributeBinding
		{
			/// <summary>
			/// 
			/// </summary>
			/// <param name="location"></param>
			/// <param name="type"></param>
			public AttributeBinding(uint location, ShaderAttributeType type)
			{
				Location = location;
				Type = type;
			}

			/// <summary>
			/// Invalid location index.
			/// </summary>
			public static readonly uint InvalidLocation = UInt32.MaxValue;

			/// <summary>
			/// Attribute location.
			/// </summary>
			public readonly uint Location;

			/// <summary>
			/// The type of the shader program attribute.
			/// </summary>
			public readonly ShaderAttributeType Type;
		}

		/// <summary>
		/// Map active uniform location with uniform name.
		/// </summary>
		private readonly Dictionary<string, AttributeBinding> _AttributesMap = new Dictionary<string, AttributeBinding>();

		#endregion

		#region Program Attributes Metadata

		/// <summary>
		/// Set a shader program attribute location.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> that specify the attribute name. This value doesn't have to match with the actual
		/// shader program attributes, but it is usually.
		/// </param>
		/// <param name="location">
		/// A <see cref="Int32"/> that specify the attribute location.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="attributeName"/> is null.
		/// </exception>
		public void SetAttributeLocation(string attributeName, int location)
		{
			if (attributeName == null)
				throw new ArgumentNullException("attributeName");

			AttributeMetadata attributeMetadata;

			if (_AttributeMetadata.TryGetValue(attributeName, out attributeMetadata) == false)
				_AttributeMetadata.Add(attributeName, attributeMetadata = new AttributeMetadata());
			attributeMetadata.Location = location;
		}

		/// <summary>
		/// Get a shader program attribute location.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> that specify the attribute name. This value doesn't have to match with the actual
		/// shader program attributes, but it is usually.
		/// </param>
		/// <returns>
		/// It returns a <see cref="Int32"/> that specify the location associated to <paramref name="attributeName"/>, if defined. In
		/// the case no semantic was associated to <paramref name="attributeName"/>, it returns -1.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="attributeName"/> is null.
		/// </exception>
		public int GetAttributeLocation(string attributeName)
		{
			if (attributeName == null)
				throw new ArgumentNullException("attributeName");

			// Extract array name
			AttributeMetadata attributeMetadata;

			if (_AttributeMetadata.TryGetValue(attributeName, out attributeMetadata))
				return (attributeMetadata.Location);

			return (-1);
		}

		/// <summary>
		/// Set a shader program attribute semantic.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> that specify the attribute name. This value doesn't have to match with the actual
		/// shader program attributes, but it is usually.
		/// </param>
		/// <param name="semantic">
		/// A <see cref="String"/> that specify the attribute semantic. It can be any value meaninfull for the application; usually
		/// it equals to the constant fields of <see cref="VertexArraySemantic"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="attributeName"/> or <paramref name="semantic"/> is null.
		/// </exception>
		public void SetAttributeSemantic(string attributeName, string semantic)
		{
			if (attributeName == null)
				throw new ArgumentNullException("attributeName");
			if (semantic == null)
				throw new ArgumentNullException("semantic");

			AttributeMetadata attributeMetadata;

			if (_AttributeMetadata.TryGetValue(attributeName, out attributeMetadata) == false)
				_AttributeMetadata.Add(attributeName, attributeMetadata = new AttributeMetadata());
			attributeMetadata.Semantic = semantic;
		}

		/// <summary>
		/// Get a shader program attribute semantic.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> that specify the attribute name. This value doesn't have to match with the actual
		/// shader program attributes, but it is usually.
		/// </param>
		/// <returns>
		/// It returns a <see cref="String"/> that specify the semantic associated to <paramref name="attributeName"/>, if defined. In
		/// the case no semantic was associated to <paramref name="attributeName"/>, it returns null.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="attributeName"/> is null.
		/// </exception>
		public string GetAttributeSemantic(string attributeName)
		{
			if (attributeName == null)
				throw new ArgumentNullException("attributeName");

			string semantic;

			// Extract array name
			Match arrayMatch = Regex.Match(attributeName, @"(?<AttributeName>\w+)\[(?<AttributeIndex>\d+)\]");
			if (arrayMatch.Success)
				attributeName = arrayMatch.Groups["AttributeName"].Value;

			AttributeMetadata attributeMetadata;

			if (_AttributeMetadata.TryGetValue(attributeName, out attributeMetadata)) {
				if (attributeMetadata.Semantic == null)
					return (null);
				if (arrayMatch.Success)
					semantic = String.Format("{0}[{1}]", attributeMetadata.Semantic, arrayMatch.Groups["AttributeIndex"].Value);

				return (attributeMetadata.Semantic);
			}

			return (null);
		}

		/// <summary>
		/// Remove a specific attribute semantic.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> that specify the attribute name. This value doesn't have to match with the actual
		/// shader program attributes.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="attributeName"/> is null.
		/// </exception>
		public void ResetAttributeSemantic(string attributeName)
		{
			if (attributeName == null)
				throw new ArgumentNullException("attributeName");

			AttributeMetadata attributeMetadata;

			if (_AttributeMetadata.TryGetValue(attributeName, out attributeMetadata))
				attributeMetadata.Semantic = null;
		}

		/// <summary>
		/// Remove all defined attribute semantics.
		/// </summary>
		public void ClearAttributesMetadata()
		{
			_AttributeMetadata.Clear();
		}

		/// <summary>
		/// Attribute metadata information.
		/// </summary>
		private class AttributeMetadata
		{
			/// <summary>
			/// The semantic associated to the attribute.
			/// </summary>
			public string Semantic;

			/// <summary>
			/// The location of attribute.
			/// </summary>
			public int Location = -1;
		}

		/// <summary>
		/// Map between program attribute names and attribute metadata.
		/// </summary>
		private readonly Dictionary<string, AttributeMetadata> _AttributeMetadata = new Dictionary<string, AttributeMetadata>();

		#endregion

		#region Program Uniform Semantic

		/// <summary>
		/// Set a shader program uniform semantic.
		/// </summary>
		/// <param name="uniformName">
		/// A <see cref="String"/> that specify the uniform name. This value doesn't have to match with the actual
		/// shader program uniforms, but it is usually.
		/// </param>
		/// <param name="semantic">
		/// A <see cref="String"/> that specify the uniform semantic. It can be any value meaninfull for the application; usually
		/// it equals to the constant fields of <see cref="ProgramUniformSemantic"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="uniformName"/> or <paramref name="semantic"/> is null.
		/// </exception>
		public void SetUniformSemantic(string uniformName, string semantic)
		{
			if (uniformName == null)
				throw new ArgumentNullException("uniformName");
			if (semantic == null)
				throw new ArgumentNullException("semantic");

			_UniformSemantic[semantic] = uniformName;
		}

		/// <summary>
		/// Remove a specific uniform semantic.
		/// </summary>
		/// <param name="semantic">
		/// A <see cref="String"/> that specify the uniform name. This value doesn't have to match with the actual
		/// shader program uniforms.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="semantic"/> is null.
		/// </exception>
		public void ResetUniformSemantic(string semantic)
		{
			if (semantic == null)
				throw new ArgumentNullException("semantic");

			_UniformSemantic.Remove(semantic);
		}

		/// <summary>
		/// Remove all defined uniform semantics.
		/// </summary>
		public void ClearUniformSemantic()
		{
			_UniformSemantic.Clear();
		}

		/// <summary>
		/// Map between program attribute names and uniform semantic.
		/// </summary>
		private readonly Dictionary<string, string> _UniformSemantic = new Dictionary<string, string>();

		#endregion

		#region Program Feedback Varyings

		/// <summary>
		/// Information about a shader feedback attribute.
		/// </summary>
		public class FeedbackBinding
		{
			/// <summary>
			/// Construct a FeedbackBinding, specifing the type of the size of the feedback attribute.
			/// </summary>
			/// <param name="type">
			/// A <see cref="ShaderAttributeType"/> that specify the type of the components of the feedback attribute.
			/// </param>
			/// <param name="size">
			/// A <see cref="UInt32"/> that specify the feedback size, in terms of <see cref="type"/>.
			/// </param>
			public FeedbackBinding(ShaderAttributeType type, uint size)
			{
				Type = type;
				Size = size;
			}

			/// <summary>
			/// The type of the shader program feedback.
			/// </summary>
			public readonly ShaderAttributeType Type;

			/// <summary>
			/// Feedback size, in terms of <see cref="Type"/>.
			/// </summary>
			public readonly uint Size;
		}

		/// <summary>
		/// Adds a feedback varying.
		/// </summary>
		/// <param name='varying'>
		/// Varying variable name.
		/// </param>
		/// <exception cref='ArgumentNullException'>
		/// Is thrown when <paramref name="varying"/> passed to a method is invalid because it is <see langword="null" /> .
		/// </exception>
		public void AddFeedbackVarying(string varying)
		{
			if (varying == null)
				throw new ArgumentNullException("varying");

			_FeedbackVaryings.Add(varying);
		}

		/// <summary>
		/// Collection of active attributes on this ShaderProgram.
		/// </summary>
		public ICollection<string> ActiveFeedbacks
		{
			get
			{
				return (_FeedbacksMap.Keys);
			}
		}

		/// <summary>
		/// Determine whether an attributes is active or not.
		/// </summary>
		/// <param name="attributeName">
		/// A <see cref="String"/> which specify the input name.
		/// </param>
		/// <returns>
		/// It returns true in the case the input named <paramref name="attributeName"/> is active.
		/// </returns>
		public bool IsActiveFeedback(string attributeName)
		{
			return (_FeedbacksMap.ContainsKey(attributeName));
		}

		/// <summary>
		/// Active attributes binding information.
		/// </summary>
		/// <param name="attributeName"></param>
		/// <returns></returns>
		internal FeedbackBinding GetActiveFeedback(string attributeName)
		{
			return (_FeedbacksMap[attributeName]);
		}

		/// <summary>
		/// The feedback varyings of this program.
		/// </summary>
		protected readonly List<string> _FeedbackVaryings = new List<string>();

		/// <summary>
		/// Map active feedback location with veretx shader (or geometry shader) output attribute name.
		/// </summary>
		private readonly Dictionary<string, FeedbackBinding> _FeedbacksMap = new Dictionary<string, FeedbackBinding>();

		#endregion

		#region Fragment Locations

		/// <summary>
		/// Set location of the fragment shader outputs.
		/// </summary>
		/// <param name="fragmentOutputName">
		/// A <see cref="String"/> that specify the fragment shader output variable to bind for a specific
		/// location.
		/// </param>
		/// <param name="location">
		/// A <see cref="Int32"/> that will be the location of the fragment data written to
		/// <paramref name="fragmentOutputName"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="fragmentOutputName"/> is null.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// Exception thrown if <paramref name="fragmentOutputName"/> starts with "gl_" (reserved name).
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// Exception thrown if the program has been already linked.
		/// </exception>
		public void SetFragmentLocation(string fragmentOutputName, int location)
		{
			if (fragmentOutputName == null)
				throw new ArgumentNullException("fragmentOutputName");
			if (fragmentOutputName.StartsWith("gl_"))
				throw new ArgumentException("reserved name");
			if (IsLinked)
				throw new InvalidOperationException("already linked");

			_FragLocations[fragmentOutputName] = location;
		}

		/// <summary>
		/// Get the location of the fragment shader output.
		/// </summary>
		/// <param name="fragOutputName"></param>
		/// <returns></returns>
		public int GetFragmentLocation(string fragOutputName)
		{
			if (fragOutputName == null)
				throw new ArgumentNullException("fragOutputName");
			if (fragOutputName.StartsWith("gl"))
				throw new ArgumentException("reserved name");

			return (_FragLocations[fragOutputName]);
		}

		/// <summary>
		/// The location of the fragment shader outputs.
		/// </summary>
		private readonly Dictionary<string, int> _FragLocations = new Dictionary<string, int>();

		/// <summary>
		/// Map between program attribute names and attribute semantic.
		/// </summary>
		private readonly Dictionary<string, string> _FragDataSemantic = new Dictionary<string, string>();

		#endregion

		#region Get Program Binary

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ctx"></param>
		/// <param name="binaryStream"></param>
		public void GetBinary(GraphicsContext ctx, Stream binaryStream, out int programCacheFormat)
		{
			CheckCurrentContext(ctx);
			CheckThisExistence(ctx);
			if (!ctx.Extensions.GetProgramBinary_ARB)
				throw new NotSupportedException("get_program_binary not supported");

			int programCacheLength;

			Gl.GetProgram(ObjectName, ProgramProperty.ProgramBinaryLength, out programCacheLength);

			byte[] programCache = new byte[programCacheLength];

			GCHandle programCacheBuffer = GCHandle.Alloc(programCache, GCHandleType.Pinned);
			try {
				Gl.GetProgramBinary(ObjectName, programCache.Length, out programCacheLength, out programCacheFormat, programCacheBuffer.AddrOfPinnedObject());
			} finally {
				programCacheBuffer.Free();
			}

			binaryStream.Write(programCache, 0, programCache.Length);
		}

		#endregion

		#region Compute

		/// <summary>
		/// Dispatch the compute program.
		/// </summary>
		/// <param name="ctx">
		/// The <see cref="GraphicsContext"/> on which the compute is dispatched.
		/// </param>
		/// <param name="x">
		/// The number of work groups to be launched in the X dimension.
		/// </param>
		/// <param name="y">
		/// The number of work groups to be launched in the Y dimension.
		/// </param>
		/// <param name="z">
		/// The number of work groups to be launched in the Z dimension.
		/// </param>
		public void Compute(GraphicsContext ctx, uint x, uint y = 1, uint z = 1)
		{
			CheckCurrentContext(ctx);
			CheckThisExistence(ctx);

			if (x * y * z > ctx.Limits.MaxComputeWorkGroupInvocations)
				throw new ArgumentException($"too much invocations, max is {ctx.Limits.MaxComputeWorkGroupInvocations}");

			// Ensure program bound
			ctx.Bind(this);
			// Ensure linked I/O
			BindImages(ctx);
			// Dispatch
			Gl.DispatchCompute(x, y, z);
		}

		#endregion

		#region Memory Barrier

		/// <summary>
		/// Defines a barrier ordering memory transactions
		/// </summary>
		/// <param name="memoryBarrierMask">
		/// Specifies the barriers to insert.
		/// </param>
		public void MemoryBarrier(MemoryBarrierMask memoryBarrierMask = MemoryBarrierMask.AllBarrierBits)
		{
			Gl.MemoryBarrier(memoryBarrierMask);
		}

		#endregion

		#region Images

		/// <summary>
		/// Link between image uniform and image unit preset.
		/// </summary>
		private class ImageUnitBinding : ImageUnitState, IDisposable
		{
			/// <summary>
			/// Construct a ImageUnitBinding.
			/// </summary>
			public ImageUnitBinding()
			{

			}

			/// <summary>
			/// Construct a ImageUnitBinding.
			/// </summary>
			/// <param name="uniformName">
			/// The <see cref="string"/> that specifies the name of the uniform.
			/// </param>
			public ImageUnitBinding(string uniformName)
			{
				if (uniformName == null)
					throw new ArgumentNullException(nameof(uniformName));
				UniformName = uniformName;
			}

			/// <summary>
			/// The name of the image uniform.
			/// </summary>
			public readonly string UniformName;

			/// <summary>
			/// Get or set the texture currently bound on texture unit.
			/// </summary>
			public Texture Texture
			{
				get { return _Texture; }
				set { Swap(value, ref _Texture); }
			}

			/// <summary>
			/// Texture currently bound on texture unit.
			/// </summary>
			private Texture _Texture;

			/// <summary>
			/// Overrides the format used for accesses to bound texture.
			/// </summary>
			public InternalFormat? OverrideInternalFormat;

			/// <summary>
			/// Format used for accesses to bound texture.
			/// </summary>
			public override InternalFormat InternalFormat
			{
				get 
				{
					// Override first
					if (OverrideInternalFormat.HasValue)
						return OverrideInternalFormat.Value;
					
					// Match texture coordinate, if any.
					Texture texture = Texture;
					if (texture != null)
						return texture.PixelLayout.ToInternalFormat();

					// Base implementation (R8)
					return base.InternalFormat;
				}
				set { base.InternalFormat = value; }
			}

			public void Dispose()
			{
				_Texture?.DecRef();
			}
		}

		public void BindImage(string uniformName, Texture texture, BufferAccess access = BufferAccess.ReadOnly, int level = 0, InternalFormat? internalFormat = null)
		{
			ImageUnitBinding imageUnitBinding = new ImageUnitBinding(uniformName);

			imageUnitBinding.Texture = texture;
			imageUnitBinding.Level = level;
			imageUnitBinding.Access = access;
			imageUnitBinding.OverrideInternalFormat = internalFormat;

			_ImageUnitBindings[uniformName] = imageUnitBinding;
		}

		public void BindImage(string uniformName, Texture3D texture, int layer, BufferAccess access = BufferAccess.ReadOnly, int level = 0, InternalFormat? internalFormat = null)
		{
			ImageUnitBinding imageUnitBinding = new ImageUnitBinding(uniformName);

			imageUnitBinding.Texture = texture;
			imageUnitBinding.Level = level;
			imageUnitBinding.Layered = true;
			imageUnitBinding.Layer = layer;
			imageUnitBinding.Access = access;
			imageUnitBinding.OverrideInternalFormat = internalFormat;

			_ImageUnitBindings[uniformName] = imageUnitBinding;
		}

		public void BindImage(string uniformName, TextureArray2D texture, int layer, BufferAccess access = BufferAccess.ReadOnly, int level = 0, InternalFormat? internalFormat = null)
		{
			ImageUnitBinding imageUnitBinding = new ImageUnitBinding(uniformName);

			imageUnitBinding.Texture = texture;
			imageUnitBinding.Level = level;
			imageUnitBinding.Layered = true;
			imageUnitBinding.Layer = layer;
			imageUnitBinding.Access = access;
			imageUnitBinding.OverrideInternalFormat = internalFormat;

			_ImageUnitBindings[uniformName] = imageUnitBinding;
		}

		public void BindImage(string uniformName, TextureCube texture, int layer, BufferAccess access = BufferAccess.ReadOnly, int level = 0, InternalFormat? internalFormat = null)
		{
			ImageUnitBinding imageUnitBinding = new ImageUnitBinding(uniformName);

			imageUnitBinding.Texture = texture;
			imageUnitBinding.Level = level;
			imageUnitBinding.Layered = true;
			imageUnitBinding.Layer = layer;
			imageUnitBinding.Access = access;
			imageUnitBinding.OverrideInternalFormat = internalFormat;

			_ImageUnitBindings[uniformName] = imageUnitBinding;
		}

		protected void BindImages(GraphicsContext ctx)
		{
			CheckCurrentContext(ctx);

			foreach (KeyValuePair<string, ImageUnitBinding> pair in _ImageUnitBindings) {
				ImageUnitBinding imageBinding = pair.Value;
				ImageUnit imageUnit = ctx.GetImageUnit(imageBinding.Texture, imageBinding);

				SetUniform(ctx, pair.Key, (int)imageUnit.Index);

				imageUnit.Bind(ctx, imageBinding.Texture, imageBinding);
			}
		}
		
		/// <summary>
		/// The image units to bind before computation.
		/// </summary>
		private readonly Dictionary<string, ImageUnitBinding> _ImageUnitBindings = new Dictionary<string, ImageUnitBinding>();

		#endregion

		#region Overrides

		/// <summary>
		/// Shader program object class.
		/// </summary>
		internal static readonly Guid ThisObjectClass = new Guid("63E57E43-9DD1-46E3-8EA0-DE87836818D7");

		/// <summary>
		/// Shader program object class.
		/// </summary>
		public override Guid ObjectClass { get { return (ThisObjectClass); } }

		/// <summary>
		/// Determine whether this ShaderProgram really exists for a specific context.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> that would have created (or a sharing one) the object. This context shall be current to
		/// the calling thread.
		/// </param>
		/// <returns>
		/// It returns a boolean value indicating whether this ShaderProgram exists in the object space of <paramref name="ctx"/>.
		/// </returns>
		/// <remarks>
		/// <para>
		/// The object existence is done by checking a valid object by its name <see cref="IGraphicsResource.ObjectName"/>. This routine will test whether
		/// <paramref name="ctx"/> has created this ShaderProgram (or is sharing with the creator).
		/// </para>
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// Exception thrown if <paramref name="ctx"/> is null.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// Exception thrown if <paramref name="ctx"/> is not current to the calling thread.
		/// </exception>
		public override bool Exists(GraphicsContext ctx)
		{
			// Object name space test (and 'ctx' sanity checks)
			if (base.Exists(ctx) == false)
				return (false);

			return (Gl.IsProgram(ObjectName));
		}

		/// <summary>
		/// Create this ShaderProgram.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for creating this object.
		/// </param>
		public override void Create(GraphicsContext ctx)
		{
			// Create default compilation, but only if necessary
			if (ReferenceEquals(CompilationParams, null))
				_CompilationParams = new ShaderCompilerContext(ctx.ShadingVersion);
			// Base implementation
			base.Create(ctx);
		}

		/// <summary>
		/// Create a ShaderProgram name.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for creating this object name.
		/// </param>
		/// <returns>
		/// It returns a valid object name for this ShaderProgram.
		/// </returns>
		protected override uint CreateName(GraphicsContext ctx)
		{
			// Create program
			return (Gl.CreateProgram());
		}

		/// <summary>
		/// Actually create this ShaderProgram resources.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for allocating resources.
		/// </param>
		protected override void CreateObject(GraphicsContext ctx)
		{
			// Debug label
			if (Identifier != null)
				ctx.DebugObjectLabel(this, Identifier);

			// Link this shader program
			if (!IsLinked) {
				if (_ProgramBinary != null) {
					Load(ctx);
				} else
					Link(ctx);
			}
		}

		/// <summary>
		/// Delete a ShaderProgram name.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for deleting this object name.
		/// </param>
		/// <param name="name">
		/// A <see cref="UInt32"/> that specify the object name to delete.
		/// </param>
		protected override void DeleteName(GraphicsContext ctx, uint name)
		{
			// Delete program
			Gl.DeleteProgram(name);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting managed/unmanaged resources.
		/// </summary>
		/// <param name="disposing">
		/// A <see cref="Boolean"/> indicating whether this method is called by <see cref="Dispose"/>. If it is false,
		/// this method is called by the finalizer.
		/// </param>
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				// Release reference to attached program objects
				_ProgramObjects.Dispose();
				// Release references to texture used as images
				foreach (KeyValuePair<string, ImageUnitBinding> pair in _ImageUnitBindings)
					pair.Value.Dispose();
			}
			// Base implementation
			base.Dispose(disposing);
		}

		#endregion

		#region IBindingResource Implementation

		/// <summary>
		/// Get the identifier of the binding point.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for binding.
		/// </param>
		int IBindingResource.GetBindingTarget(GraphicsContext ctx)
		{
			return (Gl.CURRENT_PROGRAM);
		}

		/// <summary>
		/// Bind this IBindingResource.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for binding.
		/// </param>
		void IBindingResource.Bind(GraphicsContext ctx)
		{
			CheckThisExistence(ctx);

			Gl.UseProgram(ObjectName);
		}

		/// <summary>
		/// Bind this IBindingResource.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for binding.
		/// </param>
		void IBindingResource.Unbind(GraphicsContext ctx)
		{
			CheckThisExistence(ctx);

			Gl.UseProgram(InvalidObjectName);
		}

		/// <summary>
		/// Check whether this IBindingResource is currently bound on the specified graphics context.
		/// </summary>
		/// <param name="ctx">
		/// A <see cref="GraphicsContext"/> used for querying the current binding state.
		/// </param>
		/// <returns>
		/// It returns a boolean value indicating whether this IBindingResource is currently bound on <paramref name="ctx"/>.
		/// </returns>
		bool IBindingResource.IsBound(GraphicsContext ctx)
		{
			CheckThisExistence(ctx);

			int currentProgram;
			
			Gl.Get(Gl.CURRENT_PROGRAM, out currentProgram);

			return (currentProgram == (int)ObjectName);
		}

		#endregion
	}
}
