namespace WinterRose.Reflection.TypeConverters.Builtin;

public class ByteToChar : TypeConverter<char, byte>
{
    public override byte Convert(char source)
    {
        return (byte)source;
    }
}

public class CharToByte : TypeConverter<byte, char>
{
    public override char Convert(byte source)
    {
        return (char)source;
    }
}