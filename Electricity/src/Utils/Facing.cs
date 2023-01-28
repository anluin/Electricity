using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace Electricity.Utils
{
    [Flags]
    [ProtoContract]
    public enum Facing
    {
        None = 0b_0000_0000_0000_0000_0000_0000,

        AllAll = 0b_1111_1111_1111_1111_1111_1111,
        AllNorth = EastNorth | WestNorth | UpNorth | DownNorth,
        AllEast = NorthEast | SouthEast | UpEast | DownEast,
        AllSouth = EastSouth | WestSouth | UpSouth | DownSouth,
        AllWest = NorthWest | SouthWest | UpWest | DownWest,
        AllUp = NorthUp | EastUp | SouthUp | WestUp,
        AllDown = NorthDown | EastDown | SouthDown | WestDown,

        NorthAll = NorthEast | NorthWest | NorthUp | NorthDown,
        NorthEast = 0b_1000_0000_0000_0000_0000_0000,
        NorthWest = 0b_0100_0000_0000_0000_0000_0000,
        NorthUp = 0b_0010_0000_0000_0000_0000_0000,
        NorthDown = 0b_0001_0000_0000_0000_0000_0000,

        EastAll = EastNorth | EastSouth | EastUp | EastDown,
        EastNorth = 0b_0000_1000_0000_0000_0000_0000,
        EastSouth = 0b_0000_0100_0000_0000_0000_0000,
        EastUp = 0b_0000_0010_0000_0000_0000_0000,
        EastDown = 0b_0000_0001_0000_0000_0000_0000,

        SouthAll = SouthEast | SouthWest | SouthUp | SouthDown,
        SouthEast = 0b_0000_0000_1000_0000_0000_0000,
        SouthWest = 0b_0000_0000_0100_0000_0000_0000,
        SouthUp = 0b_0000_0000_0010_0000_0000_0000,
        SouthDown = 0b_0000_0000_0001_0000_0000_0000,

        WestAll = WestNorth | WestSouth | WestUp | WestDown,
        WestNorth = 0b_0000_0000_0000_1000_0000_0000,
        WestSouth = 0b_0000_0000_0000_0100_0000_0000,
        WestUp = 0b_0000_0000_0000_0010_0000_0000,
        WestDown = 0b_0000_0000_0000_0001_0000_0000,

        UpAll = UpNorth | UpEast | UpSouth | UpWest,
        UpNorth = 0b_0000_0000_0000_0000_1000_0000,
        UpEast = 0b_0000_0000_0000_0000_0100_0000,
        UpSouth = 0b_0000_0000_0000_0000_0010_0000,
        UpWest = 0b_0000_0000_0000_0000_0001_0000,

        DownAll = DownNorth | DownEast | DownSouth | DownWest,
        DownNorth = 0b_0000_0000_0000_0000_0000_1000,
        DownEast = 0b_0000_0000_0000_0000_0000_0100,
        DownSouth = 0b_0000_0000_0000_0000_0000_0010,
        DownWest = 0b_0000_0000_0000_0000_0000_0001
    }

    public static class FacingHelper
    {
        public static Facing FromFace(BlockFacing face)
        {
            return face.Index switch
            {
                BlockFacing.indexNORTH => Facing.NorthAll,
                BlockFacing.indexEAST => Facing.EastAll,
                BlockFacing.indexSOUTH => Facing.SouthAll,
                BlockFacing.indexWEST => Facing.WestAll,
                BlockFacing.indexUP => Facing.UpAll,
                BlockFacing.indexDOWN => Facing.DownAll,
                _ => Facing.None
            };
        }

        public static Facing FromDirection(BlockFacing direction)
        {
            return direction.Index switch
            {
                BlockFacing.indexNORTH => Facing.AllNorth,
                BlockFacing.indexEAST => Facing.AllEast,
                BlockFacing.indexSOUTH => Facing.AllSouth,
                BlockFacing.indexWEST => Facing.AllWest,
                BlockFacing.indexUP => Facing.AllUp,
                BlockFacing.indexDOWN => Facing.AllDown,
                _ => Facing.None
            };
        }

        public static Facing From(BlockFacing face, BlockFacing direction)
        {
            return FromFace(face) & FromDirection(direction);
        }

        public static Facing OppositeDirection(Facing self)
        {
            var result = Facing.None;

            result |= (self & Facing.NorthEast) != 0 ? Facing.NorthWest : Facing.None;
            result |= (self & Facing.NorthWest) != 0 ? Facing.NorthEast : Facing.None;
            result |= (self & Facing.NorthUp) != 0 ? Facing.NorthDown : Facing.None;
            result |= (self & Facing.NorthDown) != 0 ? Facing.NorthUp : Facing.None;

            result |= (self & Facing.EastNorth) != 0 ? Facing.EastSouth : Facing.None;
            result |= (self & Facing.EastSouth) != 0 ? Facing.EastNorth : Facing.None;
            result |= (self & Facing.EastUp) != 0 ? Facing.EastDown : Facing.None;
            result |= (self & Facing.EastDown) != 0 ? Facing.EastUp : Facing.None;

            result |= (self & Facing.SouthEast) != 0 ? Facing.SouthWest : Facing.None;
            result |= (self & Facing.SouthWest) != 0 ? Facing.SouthEast : Facing.None;
            result |= (self & Facing.SouthUp) != 0 ? Facing.SouthDown : Facing.None;
            result |= (self & Facing.SouthDown) != 0 ? Facing.SouthUp : Facing.None;

            result |= (self & Facing.UpNorth) != 0 ? Facing.UpSouth : Facing.None;
            result |= (self & Facing.UpEast) != 0 ? Facing.UpWest : Facing.None;
            result |= (self & Facing.UpSouth) != 0 ? Facing.UpNorth : Facing.None;
            result |= (self & Facing.UpWest) != 0 ? Facing.UpEast : Facing.None;

            result |= (self & Facing.DownNorth) != 0 ? Facing.DownSouth : Facing.None;
            result |= (self & Facing.DownEast) != 0 ? Facing.DownWest : Facing.None;
            result |= (self & Facing.DownSouth) != 0 ? Facing.DownNorth : Facing.None;
            result |= (self & Facing.DownWest) != 0 ? Facing.DownEast : Facing.None;

            return result;
        }

        public static IEnumerable<BlockFacing> Faces(Facing self)
        {
            return new[]
            {
                (self & Facing.NorthAll) != 0 ? BlockFacing.NORTH : null,
                (self & Facing.EastAll) != 0 ? BlockFacing.EAST : null,
                (self & Facing.SouthAll) != 0 ? BlockFacing.SOUTH : null,
                (self & Facing.WestAll) != 0 ? BlockFacing.WEST : null,
                (self & Facing.UpAll) != 0 ? BlockFacing.UP : null,
                (self & Facing.DownAll) != 0 ? BlockFacing.DOWN : null
            }.Where(face => face is { }).Select(face => face!);
        }

        public static IEnumerable<BlockFacing> Directions(Facing self)
        {
            return new[]
            {
                (self & Facing.AllNorth) != 0 ? BlockFacing.NORTH : null,
                (self & Facing.AllEast) != 0 ? BlockFacing.EAST : null,
                (self & Facing.AllSouth) != 0 ? BlockFacing.SOUTH : null,
                (self & Facing.AllWest) != 0 ? BlockFacing.WEST : null,
                (self & Facing.AllUp) != 0 ? BlockFacing.UP : null,
                (self & Facing.AllDown) != 0 ? BlockFacing.DOWN : null
            }.Where(face => face is { }).Select(face => face!);
        }

        public static Facing FullFace(Facing self)
        {
            return Faces(self).Aggregate(Facing.None, (current, face) => current | FromFace(face));
        }

        public static int Count(Facing self)
        {
            var count = 0;

            while (self != Facing.None)
            {
                count++;
                self &= self - 1;
            }

            return count;
        }
    }
}