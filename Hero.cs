using System.Collections.Generic;

namespace RedisConsoleApp
{
    public class Hero
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string Manga { get; set; }

        public List<Friend> Friends { get; set; }
    }
}
