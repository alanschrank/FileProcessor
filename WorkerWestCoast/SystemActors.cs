﻿using Akka.Actor;

namespace WorkerWestCoast
{

    public static class SystemActors
    {
        public static ActorSystem ClusterSystem;
        public static IActorRef Mediator = ActorRefs.Nobody;
    }
}