﻿using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Cluster.Tools.Singleton;
using Akka.DI.Core;
using Akka.DI.Ninject;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using ProcessorWestCoast.Actors;
using SharedLibrary.Actors;
using SharedLibrary.Helpers;
using SharedLibrary.Repos;

namespace ProcessorWestCoast
{
    public class MyService
    {
        public void Start()
        {
            SystemActors.ClusterSystem = SystemHostFactory.Launch();

            // Create and build the container
            var container = new Ninject.StandardKernel();
            container.Bind<IFileProcessorRepository>().To(typeof(FileProcessorRepository)).InTransientScope();
            container.Bind<DistributedPubSub>().ToConstant(DistributedPubSub.Get(SystemActors.ClusterSystem));

            // Create the dependency resolver for the actor system
            IDependencyResolver resolver = new NinjectDependencyResolver(container, SystemActors.ClusterSystem);

            var pbm = PetabridgeCmd.Get(SystemActors.ClusterSystem);
            pbm.RegisterCommandPalette(ClusterCommands.Instance); // enable cluster management commands
            pbm.Start();

            SystemActors.SettingsWatcherRef = SystemActors.ClusterSystem.ActorOf(SystemActors.ClusterSystem.DI().Props<DatabaseWatcherActor>(), "DatabaseWatcher");
            SystemActors.Mediator = DistributedPubSub.Get(SystemActors.ClusterSystem).Mediator;

            SystemActors.LocationManagerActorRef = SystemActors.ClusterSystem.ActorOf(ClusterSingletonManager.Props(
                    singletonProps: Props.Create(() => new LocationManagerActor()),         // Props used to create actor singleton
                    terminationMessage: PoisonPill.Instance,                  // message used to stop actor gracefully
                    settings: ClusterSingletonManagerSettings.Create(SystemActors.ClusterSystem).WithRole(StaticMethods.GetServiceWorkerRole())),// cluster singleton manager settings
                name: ActorPaths.SingletonManagerActor.Name);

            SystemActors.LocationManagerProxyRef = SystemActors.ClusterSystem.ActorOf(ClusterSingletonProxy.Props(
                    singletonManagerPath: ActorPaths.SingletonManagerActor.Path,
                    settings: ClusterSingletonProxySettings.Create(SystemActors.ClusterSystem).WithRole(StaticMethods.GetServiceWorkerRole())),
                name: ActorPaths.SingletonManagerProxy.Name);
        }

        /// <summary>
        /// Task completes once the Lighthouse <see cref="ActorSystem"/> has terminated.
        /// </summary>
        /// <remarks>TerminationHandle
        /// Doesn't actually invoke termination. Need to call <see cref="StopAsync"/> for that.
        /// </remarks>
        public Task TerminationHandle => SystemActors.ClusterSystem.WhenTerminated;

        public async Task StopAsync()
        {
            await CoordinatedShutdown.Get(SystemActors.ClusterSystem).Run();
        }
    }
}
