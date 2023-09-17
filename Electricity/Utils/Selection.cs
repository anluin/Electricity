using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Utils {
    public class Selection {
        private readonly bool didOffset;
        private readonly Vec3d hitPosition;

        public Selection(Vec3d hitPosition, bool didOffset) {
            this.hitPosition = hitPosition;
            this.didOffset = didOffset;
        }

        public Selection(BlockSelection blockSelection) {
            this.hitPosition = blockSelection.HitPosition;
            this.didOffset = blockSelection.DidOffset;
        }

        public Vec2d Position2D {
            get {
                switch (this.Face.Index) {
                    case BlockFacing.indexNORTH:
                    case BlockFacing.indexSOUTH:
                        return new Vec2d(this.hitPosition.X, this.hitPosition.Y);
                    case BlockFacing.indexEAST:
                    case BlockFacing.indexWEST:
                        return new Vec2d(this.hitPosition.Y, this.hitPosition.Z);
                    case BlockFacing.indexUP:
                    case BlockFacing.indexDOWN:
                        return new Vec2d(this.hitPosition.X, this.hitPosition.Z);
                    default:
                        throw new Exception();
                }
            }
        }

        public BlockFacing Direction {
            get {
                switch (this.Face.Index) {
                    case BlockFacing.indexNORTH:
                    case BlockFacing.indexSOUTH:
                        return this.DirectionHelper(BlockFacing.EAST, BlockFacing.WEST, BlockFacing.UP, BlockFacing.DOWN);
                    case BlockFacing.indexEAST:
                    case BlockFacing.indexWEST:
                        return this.DirectionHelper(BlockFacing.UP, BlockFacing.DOWN, BlockFacing.SOUTH, BlockFacing.NORTH);
                    case BlockFacing.indexUP:
                    case BlockFacing.indexDOWN:
                        return this.DirectionHelper(
                            BlockFacing.EAST,
                            BlockFacing.WEST,
                            BlockFacing.SOUTH,
                            BlockFacing.NORTH
                        );
                    default:
                        throw new Exception();
                }
            }
        }

        public BlockFacing Face {
            get {
                var normalize = this.hitPosition.SubCopy(0.5f, 0.5f, 0.5f);

                if (normalize.X > normalize.Y && normalize.X > normalize.Z && normalize.X > -normalize.Y && normalize.X > -normalize.Z) {
                    return this.didOffset
                        ? BlockFacing.WEST
                        : BlockFacing.EAST;
                }

                if (normalize.X < normalize.Y && normalize.X < normalize.Z && normalize.X < -normalize.Y && normalize.X < -normalize.Z) {
                    return this.didOffset
                        ? BlockFacing.EAST
                        : BlockFacing.WEST;
                }

                if (normalize.Z > normalize.Y && normalize.Z > normalize.X && normalize.Z > -normalize.Y && normalize.Z > -normalize.X) {
                    return this.didOffset
                        ? BlockFacing.NORTH
                        : BlockFacing.SOUTH;
                }

                if (normalize.Z < normalize.Y && normalize.Z < normalize.X && normalize.Z < -normalize.Y && normalize.Z < -normalize.X) {
                    return this.didOffset
                        ? BlockFacing.SOUTH
                        : BlockFacing.NORTH;
                }

                if (normalize.Y > normalize.X && normalize.Y > normalize.Z && normalize.Y > -normalize.X && normalize.Y > -normalize.Z) {
                    return this.didOffset
                        ? BlockFacing.DOWN
                        : BlockFacing.UP;
                }

                if (normalize.Y < normalize.X && normalize.Y < normalize.Z && normalize.Y < -normalize.X && normalize.Y < -normalize.Z) {
                    return this.didOffset
                        ? BlockFacing.UP
                        : BlockFacing.DOWN;
                }

                throw new Exception();
            }
        }

        public Facing Facing => FacingHelper.From(this.Face, this.Direction);

        private static Vec2d Rotate(Vec2d point, Vec2d origin, double angle) {
            return new Vec2d(
                GameMath.Cos(angle) * (point.X - origin.X) - GameMath.Sin(angle) * (point.Y - origin.Y) + origin.X,
                GameMath.Sin(angle) * (point.X - origin.X) + GameMath.Cos(angle) * (point.Y - origin.Y) + origin.Y
            );
        }

        private BlockFacing DirectionHelper(params BlockFacing[] mapping) {
            var hitPosition = Rotate(this.Position2D, new Vec2d(0.5, 0.5), 45.0 * GameMath.DEG2RAD);

            if (hitPosition.X > 0.5 && hitPosition.Y > 0.5) {
                return mapping[0];
            }

            if (hitPosition.X < 0.5 && hitPosition.Y < 0.5) {
                return mapping[1];
            }

            if (hitPosition.X < 0.5 && hitPosition.Y > 0.5) {
                return mapping[2];
            }

            if (hitPosition.X > 0.5 && hitPosition.Y < 0.5) {
                return mapping[3];
            }

            throw new Exception();
        }
    }
}
