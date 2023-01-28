namespace Electricity.Utils
{
    public static class FloatHelper
    {
        public static float Remap(float self, float fromSource, float toSource, float fromTarget, float toTarget)
        {
            return (self - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }
    }
}