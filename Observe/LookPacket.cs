using System.Buffers.Binary;
using CodeTalker.Packets;
using UnityEngine;

namespace Marioalexsan.Observe;

internal class LookPacket : BinaryPacketBase
{
    private const string Signature = $"{ModInfo.GUID}.{nameof(LookPacket)}";
    public override string PacketSignature => Signature;

    public Quaternion CameraRotation;
    public uint TargetNetId;
    public bool VanillaMode;
    public bool OwlMode;
    public LookSpeed LookSpeed;
    public LookDirection OverrideDirection; // Only used for secondary behaviour!
    
    public bool IsValid = true;
    
    public override byte[] Serialize()
    {
        var data = new byte[32]; // Extra padding with zeroes - might be useful for future versions
        var span = data.AsSpan();

        BinaryPrimitives.TryWriteInt32LittleEndian(span, BitConverter.SingleToInt32Bits(CameraRotation.x));
        BinaryPrimitives.TryWriteInt32LittleEndian(span[4..], BitConverter.SingleToInt32Bits(CameraRotation.y));
        BinaryPrimitives.TryWriteInt32LittleEndian(span[8..], BitConverter.SingleToInt32Bits(CameraRotation.z));
        BinaryPrimitives.TryWriteInt32LittleEndian(span[12..], BitConverter.SingleToInt32Bits(CameraRotation.w));
        BinaryPrimitives.TryWriteUInt32LittleEndian(span[16..], TargetNetId);
        BitConverter.TryWriteBytes(span[20..], VanillaMode);
        
        // Added in 1.1.0
        BitConverter.TryWriteBytes(span[21..], OwlMode);
        span[22] = (byte)LookSpeed;
        span[23] = (byte)OverrideDirection;

        return data;
    }

    public override void Deserialize(byte[] data)
    {
        // Current version packets should have at least 32 bytes if they're okay
        if (data.Length < 32)
        {
            IsValid = false;
            return;
        }
        
        var span = data.AsSpan();

        CameraRotation.x = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span));
        CameraRotation.y = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span[4..]));
        CameraRotation.z = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span[8..]));
        CameraRotation.w = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span[12..]));
        TargetNetId = BinaryPrimitives.ReadUInt32LittleEndian(span[16..]);
        VanillaMode = BitConverter.ToBoolean(span[20..]);
        
        // Added in 1.1.0
        OwlMode = BitConverter.ToBoolean(span[21..]);
        LookSpeed = (LookSpeed)span[22];
        OverrideDirection = (LookDirection)span[23];
    }

    public static readonly LookPacket Instance = new();
}