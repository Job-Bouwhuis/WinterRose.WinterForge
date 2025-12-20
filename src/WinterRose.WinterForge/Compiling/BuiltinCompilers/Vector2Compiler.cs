using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Compiling.BuiltinCompilers;

public sealed class Vector2Compiler : CustomValueCompiler<Vector2>
{
    public override void Compile(BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public override Vector2 Decompile(BinaryReader reader)
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        return new Vector2(x, y);
    }
}

public sealed class Vector3Compiler : CustomValueCompiler<Vector3>
{
    public override void Compile(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    public override Vector3 Decompile(BinaryReader reader)
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        return new Vector3(x, y, z);
    }
}

public sealed class Vector4Compiler : CustomValueCompiler<Vector4>
{
    public override void Compile(BinaryWriter writer, Vector4 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    public override Vector4 Decompile(BinaryReader reader)
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        float w = reader.ReadSingle();
        return new Vector4(x, y, z, w);
    }
}

public sealed class QuaternionCompiler : CustomValueCompiler<Quaternion>
{
    public override void Compile(BinaryWriter writer, Quaternion value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    public override Quaternion Decompile(BinaryReader reader)
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        float w = reader.ReadSingle();
        return new Quaternion(x, y, z, w);
    }
}

public sealed class Matrix4x4Compiler : CustomValueCompiler<Matrix4x4>
{
    public override void Compile(BinaryWriter writer, Matrix4x4 value)
    {
        writer.Write(value.M11); writer.Write(value.M12); writer.Write(value.M13); writer.Write(value.M14);
        writer.Write(value.M21); writer.Write(value.M22); writer.Write(value.M23); writer.Write(value.M24);
        writer.Write(value.M31); writer.Write(value.M32); writer.Write(value.M33); writer.Write(value.M34);
        writer.Write(value.M41); writer.Write(value.M42); writer.Write(value.M43); writer.Write(value.M44);
    }

    public override Matrix4x4 Decompile(BinaryReader reader)
    {
        float m11 = reader.ReadSingle(); 
        float m12 = reader.ReadSingle();
        float m13 = reader.ReadSingle(); 
        float m14 = reader.ReadSingle();
        
        float m21 = reader.ReadSingle(); 
        float m22 = reader.ReadSingle(); 
        float m23 = reader.ReadSingle();
        float m24 = reader.ReadSingle();
        
        float m31 = reader.ReadSingle(); 
        float m32 = reader.ReadSingle(); 
        float m33 = reader.ReadSingle(); 
        float m34 = reader.ReadSingle();
        
        float m41 = reader.ReadSingle(); 
        float m42 = reader.ReadSingle();
        float m43 = reader.ReadSingle(); 
        float m44 = reader.ReadSingle();

        return new Matrix4x4(
            m11, m12, m13, m14,
            m21, m22, m23, m24,
            m31, m32, m33, m34,
            m41, m42, m43, m44
        );
    }
}

public class ColorCompiler : CustomValueCompiler<Color>
{
    public override void Compile(BinaryWriter writer, Color value)
    {
        int packed = 0;
        packed |= value.R << 24;
        packed |= value.G << 16;
        packed |= value.B << 8;
        packed |= value.A;
        writer.Write(packed);
    }

    public override Color Decompile(BinaryReader reader)
    {
        int packed = reader.ReadInt32();
        byte r = (byte)((packed >> 24) & 0xFF);
        byte g = (byte)((packed >> 16) & 0xFF);
        byte b = (byte)((packed >> 8) & 0xFF);
        byte a = (byte)(packed & 0xFF);

        return Color.FromArgb(a, r, g, b);
    }

}


