namespace OdinEye.Models.Proto
{
    using ProtoBuf;
    using System;
    using System.Collections.Generic;

    [ProtoContract]
    public class GameEvent : Message
    {
        [ProtoMember(1)]
        public string Message { get; set; }
        
        [ProtoMember(2)]
        public EventType Type { get; set; }
        
        [ProtoMember(4)]
        public Player Player { get; set; }
        
        [ProtoMember(5)]
        public IDictionary<string, object> Data { get; set; }

        // Structured details for the event feed (GET /events, ODINEYE-35):
        // enemy prefab and level for a kill, positions for a bed event, ...
        // Deliberately NOT [ProtoMember]: protobuf-net cannot represent an
        // `object`-valued dictionary, and this event is also serialized to
        // the /activity WebSocket as protobuf -- a populated Data would risk
        // breaking every broadcast. The feed renders these as JSON "Data".
        [ProtoIgnore]
        public IDictionary<string, object> Details { get; set; }

        public static GameEvent New(EventType type, string message, Player player = null) =>
            new GameEvent
            {
                CreatedDate = DateTime.UtcNow,
                Type = type,
                Message = message,
                Player = player
            };

        public static GameEvent New(EventType type, string message, Player player, IDictionary<string, object> details) =>
            new GameEvent
            {
                CreatedDate = DateTime.UtcNow,
                Type = type,
                Message = message,
                Player = player,
                Details = details
            };
    }
}