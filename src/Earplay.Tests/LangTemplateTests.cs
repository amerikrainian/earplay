using System;
using System.IO;
using Earplay.Core.Strings;
using Xunit;

namespace Earplay.Tests
{
    public class LangTemplateTests
    {
        /// <summary>lang/en.txt is <see cref="Strings.DumpTemplate"/>, so the starting point for an
        /// override file never lags the table. After adding or rewording a string, regenerate it: run
        /// this test with EARPLAY_WRITE_LANG_TEMPLATE=1.</summary>
        [Fact]
        public void The_English_file_is_the_generated_template()
        {
            string path = Path.Combine(LangDir(), "en.txt");
            if (Environment.GetEnvironmentVariable("EARPLAY_WRITE_LANG_TEMPLATE") == "1")
                File.WriteAllText(path, Strings.DumpTemplate());
            Assert.Equal(Strings.DumpTemplate(), File.ReadAllText(path).Replace("\r\n", "\n"));
        }

        private static string LangDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Earplay.slnx"))) dir = dir.Parent;
            Assert.True(dir != null, "the repository root was not found above " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "lang");
        }
    }
}
