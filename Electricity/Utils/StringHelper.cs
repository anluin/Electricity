namespace Electricity.Utils {
    public static class StringHelper {
        public static string Progressbar(float percentage) {
            var temp = "";

            for (var index = 0; index < 16; ++index) {
                temp += index >= (percentage * 16.0f) / 100.0f
                    ? '□'
                    : '■';
            }

            return temp.Insert(8, " " + ((int)percentage).ToString().PadLeft(3, ' ') + "% ");
        }
    }
}
