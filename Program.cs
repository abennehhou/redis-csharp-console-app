using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

namespace RedisConsoleApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Make sure that the Redis service is up and running
            // You can open a Redis CLI and type monitor to check all the requests that are sent.

            TestRedisLowLevelAbstractionClient();
            Console.WriteLine(Environment.NewLine);

            TestRedisClientSimpleData();
            Console.WriteLine(Environment.NewLine);

            TestRedisClientComplexData();
            Console.WriteLine(Environment.NewLine);

            TestRedisTransactions();
            Console.WriteLine(Environment.NewLine);

            TestSubscribeAndPublish();
            Console.WriteLine(Environment.NewLine);

            Console.WriteLine("--- THE END ---");
            Console.ReadLine();
        }

        /// <summary>
        ///     Test using native redis client. Contains all the commands that are available in the Redis CLI.
        /// </summary>
        private static void TestRedisLowLevelAbstractionClient()
        {
            Console.WriteLine("****** Testing RedisNativeClient ******");
            // Create key with service stack format
            var key = "urn:messages:1";
            using (IRedisNativeClient client = new RedisNativeClient())
            {
                var message = "Hello World <3";
                Console.WriteLine($"Setting value '{message}' to key '{key}'.");
                client.Set(key, Encoding.UTF8.GetBytes(message));
            }

            using (IRedisNativeClient client = new RedisNativeClient())
            {
                var result = Encoding.UTF8.GetString(client.Get(key));
                Console.WriteLine($"Value '{result}' retrieved from key '{key}'.");
            }
            Console.WriteLine("Test for RedisNativeClient ended.");
        }

        /// <summary>
        ///     Test using the redis client.
        /// </summary>
        private static void TestRedisClientSimpleData()
        {
            Console.WriteLine("****** Testing RedisClient ******");
            // Create key with service stack format
            var key = "urn:characters";
            using (IRedisClient client = new RedisClient())
            {
                Console.WriteLine($"Setting a list of 3 values in key '{key}'.");
                var names = client.Lists[key];
                names.Clear(); // Does a LTRIM (trim) with -1, 0
                names.Add("Pikachu"); // Does a RPUSH (right push)
                names.Add("Monkey D Luffy");
                names.Add("Yagami Light");
            }

            using (IRedisClient client = new RedisClient())
            {
                // Create key with service stack format
                var result = client.Lists[key];
                Console.WriteLine($"values: [{string.Join(",", result)}] returned for key '{key}'.");
                // Does a LLEN (get length) then LRANGE (get range) with -1, 0
            }
            Console.WriteLine("Test for RedisClient ended.");
        }

        /// <summary>
        ///     Test using the typed redis client.
        /// </summary>
        private static void TestRedisClientComplexData()
        {
            Console.WriteLine("****** Testing RedisTypedClient ******");
            long id;
            using (var client = new RedisClient())
            {
                IRedisTypedClient<Hero> heroClient = new RedisTypedClient<Hero>(client);
                var hero = new Hero
                {
                    Id = heroClient.GetNextSequence(), // Will call INCR on "seq:Hero"
                    Manga = "OnePunch-Man",
                    Name = "Saitama",
                    Friends = new List<Friend>
                    {
                        new Friend {Name = "Genos"},
                        new Friend {Name = "Silver Fang"}
                    }
                };

                var storedHero = heroClient.Store(hero);
                // "Will call a SET on "urn:Hero:[SequenceNumber]" with hero serialized as JSON
                // Then calls SADD "ids:Hero" [SequenceNumber], used ***like*** an index (not a real index)
                id = storedHero.Id;
                Console.WriteLine($"Hero stored, its identifier is {id}.");
            }

            using (var client = new RedisClient())
            {
                IRedisTypedClient<Hero> heroClient = new RedisTypedClient<Hero>(client);
                var hero = heroClient.GetById(id);
                var heroFriendsStr = hero.Friends == null ? "null" : string.Join(",", hero.Friends.Select(x => x.Name));
                Console.WriteLine($"Retrieved hero: #{hero.Id}, name: '{hero.Name}', friends: [{heroFriendsStr}].");
            }
            Console.WriteLine("Test for RedisTypedClient ended.");
        }

        /// <summary>
        ///     Test a simple transaction.
        /// </summary>
        private static void TestRedisTransactions()
        {
            Console.WriteLine("****** Testing Redis transactions ******");
            using (IRedisClient client = new RedisClient())
            {
                var key = "test-transaction-key";

                var transaction = client.CreateTransaction();
                transaction.QueueCommand(myclient => myclient.Set(key, 1));
                transaction.QueueCommand(myclient => myclient.Increment(key, 2));
                transaction.Commit(); // The Commit will call: MULTI, then SET, then INCRBY, then EXEC.
                var result = client.Get<int>(key);
                Console.WriteLine($"Result: {result}, expected: 3.");
            }
            Console.WriteLine("Test for Redis transactions ended.");
        }

        /// <summary>
        ///     Test for subscribing to a channel and publishing a message in a channel.
        /// </summary>
        private static void TestSubscribeAndPublish()
        {
            Console.WriteLine("****** Testing Subscribe and publish ******");
            var myChannel = "news";
            var myMessage = "my message";

            var task = Task.Factory.StartNew(() =>
            {
                using (IRedisClient client = new RedisClient())
                {
                    var subscription = client.CreateSubscription();
                    subscription.OnMessage =
                        (channel, message) => Console.WriteLine($"Channel '{channel}' - received message: '{message}'.");
                    Console.WriteLine($"Subscribing to '{myChannel}' channel.");
                    subscription.SubscribeToChannels(myChannel);
                    // Here, we wait for incoming messages.
                }
            });

            var cancellationTimeoutInSeconds = 15;
            Thread.Sleep(1000);
            Console.WriteLine($"Please wait {cancellationTimeoutInSeconds} seconds.");
            using (IRedisClient client = new RedisClient())
            {
                Console.WriteLine($"Publishing message '{myMessage} to '{myChannel}' channel.");
                client.PublishMessage(myChannel, myMessage);
                // You can also test the Subscribe by opening a Redis client CLI and typing "publish news [a message here]."
                // To test the Publish, you can open a Redis client CLI and type "subscribe news"
            }
            Task.WaitAll(new[] { task }, TimeSpan.FromSeconds(cancellationTimeoutInSeconds));
            Console.WriteLine($"Subscribe and publish test cancelled after {cancellationTimeoutInSeconds} seconds.");
        }
    }
}