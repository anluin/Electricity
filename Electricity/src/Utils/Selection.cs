using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Utils
{
    public class Selection
    {
        private readonly bool _didOffset;
        private readonly Vec3d _hitPosition;

        public Selection(Vec3d hitPosition, bool didOffset)
        {
            _hitPosition = hitPosition;
            _didOffset = didOffset;
        }

        public Selection(BlockSelection blockSelection)
        {
            _hitPosition = blockSelection.HitPosition;
            _didOffset = blockSelection.DidOffset;
        }

        public Vec2d Position2d
        {
            get
            {
                switch (Face.Index)
                {
                    case BlockFacing.indexNORTH:
                    case BlockFacing.indexSOUTH:
                        return new Vec2d(_hitPosition.X, _hitPosition.Y);
                    case BlockFacing.indexEAST:
                    case BlockFacing.indexWEST:
                        return new Vec2d(_hitPosition.Y, _hitPosition.Z);
                    case BlockFacing.indexUP:
                    case BlockFacing.indexDOWN:
                        return new Vec2d(_hitPosition.X, _hitPosition.Z);
                    default:
                        throw new Exception();
                }
            }
        }

        public BlockFacing Direction
        {
            get
            {
                switch (Face.Index)
                {
                    case BlockFacing.indexNORTH:
                    case BlockFacing.indexSOUTH:
                        return DirectionHelper(BlockFacing.EAST, BlockFacing.WEST, BlockFacing.UP, BlockFacing.DOWN);
                    case BlockFacing.indexEAST:
                    case BlockFacing.indexWEST:
                        return DirectionHelper(BlockFacing.UP, BlockFacing.DOWN, BlockFacing.SOUTH, BlockFacing.NORTH);
                    case BlockFacing.indexUP:
                    case BlockFacing.indexDOWN:
                        return DirectionHelper(BlockFacing.EAST, BlockFacing.WEST, BlockFacing.SOUTH,
                            BlockFacing.NORTH);
                    default:
                        throw new Exception();
                }
            }
        }

        public BlockFacing Face
        {
            get
            {
                var normalize = _hitPosition.SubCopy(0.5f, 0.5f, 0.5f);

                if (normalize.X > normalize.Y && normalize.X > normalize.Z && normalize.X > -normalize.Y &&
                    normalize.X > -normalize.Z) return _didOffset ? BlockFacing.WEST : BlockFacing.EAST;

                if (normalize.X < normalize.Y && normalize.X < normalize.Z && normalize.X < -normalize.Y &&
                    normalize.X < -normalize.Z) return _didOffset ? BlockFacing.EAST : BlockFacing.WEST;

                if (normalize.Z > normalize.Y && normalize.Z > normalize.X && normalize.Z > -normalize.Y &&
                    normalize.Z > -normalize.X) return _didOffset ? BlockFacing.NORTH : BlockFacing.SOUTH;

                if (normalize.Z < normalize.Y && normalize.Z < normalize.X && normalize.Z < -normalize.Y &&
                    normalize.Z < -normalize.X) return _didOffset ? BlockFacing.SOUTH : BlockFacing.NORTH;

                if (normalize.Y > normalize.X && normalize.Y > normalize.Z && normalize.Y > -normalize.X &&
                    normalize.Y > -normalize.Z) return _didOffset ? BlockFacing.DOWN : BlockFacing.UP;

                if (normalize.Y < normalize.X && normalize.Y < normalize.Z && normalize.Y < -normalize.X &&
                    normalize.Y < -normalize.Z) return _didOffset ? BlockFacing.UP : BlockFacing.DOWN;

                throw new Exception();
            }
        }

        public Facing Facing => FacingHelper.From(Face, Direction);

        private static Vec2d Rotate(Vec2d point, Vec2d origin, double angle)
        {
            return new Vec2d(
                GameMath.Cos(angle) * (point.X - origin.X) - GameMath.Sin(angle) * (point.Y - origin.Y) + origin.X,
                GameMath.Sin(angle) * (point.X - origin.X) + GameMath.Cos(angle) * (point.Y - origin.Y) + origin.Y
            );
        }

        private BlockFacing DirectionHelper(params BlockFacing[] mapping)
        {
            var hitPosition = Rotate(Position2d, new Vec2d(0.5, 0.5), 45.0 * GameMath.DEG2RAD);

            if (hitPosition.X > 0.5 && hitPosition.Y > 0.5) return mapping[0];
            if (hitPosition.X < 0.5 && hitPosition.Y < 0.5) return mapping[1];
            if (hitPosition.X < 0.5 && hitPosition.Y > 0.5) return mapping[2];
            if (hitPosition.X > 0.5 && hitPosition.Y < 0.5) return mapping[3];

            throw new Exception();
        }
    }
}