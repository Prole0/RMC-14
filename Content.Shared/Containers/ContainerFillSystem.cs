using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Prototypes;
using Content.Shared._RMC14.Storage;
using Content.Shared.EntityTable;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared.Containers;

public sealed class ContainerFillSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContainerFillComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EntityTableContainerFillComponent, MapInitEvent>(OnTableMapInit);
    }

    private void OnMapInit(EntityUid uid, ContainerFillComponent component, MapInitEvent args)
    {
        if (!TryComp(uid, out ContainerManagerComponent? containerComp))
            return;

        var xform = Transform(uid);
        var coords = new EntityCoordinates(uid, Vector2.Zero);

        foreach (var (contaienrId, prototypes) in component.Containers)
        {
            if (!_containerSystem.TryGetContainer(uid, contaienrId, out var container, containerComp))
            {
                Log.Error($"Entity {ToPrettyString(uid)} with a {nameof(ContainerFillComponent)} is missing a container ({contaienrId}).");
                continue;
            }

            foreach (var proto in prototypes)
            {
                var ent = Spawn(proto, coords);
                if (!_containerSystem.Insert(ent, container, containerXform: xform))
                {
                    var alreadyContained = container.ContainedEntities.Count > 0 ? string.Join("\n", container.ContainedEntities.Select(e => $"\t - {ToPrettyString(e)}")) : "< empty >";
                    // RMC14
                    if (CMPrototypeExtensions.FilterCM)
                        Log.Error($"Entity {ToPrettyString(uid)} with a {nameof(ContainerFillComponent)} failed to insert an entity: {ToPrettyString(ent)}.\nCurrent contents:\n{alreadyContained}");
                    _transform.AttachToGridOrMap(ent);
                    break;
                }
            }
        }
    }

    private void OnTableMapInit(Entity<EntityTableContainerFillComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out ContainerManagerComponent? containerComp))
            return;

        if (TerminatingOrDeleted(ent) || !Exists(ent))
            return;

        var xform = Transform(ent);
        var coords = new EntityCoordinates(ent, Vector2.Zero);

        var storage = CompOrNull<StorageComponent>(ent);
        foreach (var (containerId, table) in ent.Comp.Containers)
        {
            if (!_containerSystem.TryGetContainer(ent, containerId, out var container, containerComp))
            {
                Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(EntityTableContainerFillComponent)} is missing a container ({containerId}).");
                continue;
            }

            var spawns = _entityTable.GetSpawns(table);
            foreach (var proto in spawns)
            {
                // RMC14
                EntityUid spawn;
                try
                {
                    spawn = Spawn(proto, coords);
                }
                catch(Exception e)
                {
                    Log.Error($"Error spawning {proto} at {coords}:\n{e}");
                    continue;
                }
                // RMC14

                if (storage != null && TryComp(spawn, out ItemComponent? item))
                {
                    var ev = new CMStorageItemFillEvent((spawn, item), storage);
                    RaiseLocalEvent(ent, ref ev);
                }

                if (!_containerSystem.Insert(spawn, container, containerXform: xform))
                {
                    var alreadyContained = container.ContainedEntities.Count > 0 ? string.Join("\n", container.ContainedEntities.Select(e => $"\t - {ToPrettyString(e)}")) : "< empty >";
                    // RMC14
                    if (CMPrototypeExtensions.FilterCM)
                        Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(EntityTableContainerFillComponent)} failed to insert an entity: {ToPrettyString(spawn)}.\nCurrent contents:\n{alreadyContained}");
                    _transform.AttachToGridOrMap(spawn);
                    break;
                }
            }
        }
    }
}
