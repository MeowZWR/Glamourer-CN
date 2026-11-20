using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.State;
using Luna;
using Penumbra.GameData.Interop;

namespace Glamourer.Automation;

public sealed class CompanionAutoApplier : IRequiredService, IDisposable
{
    private readonly Configuration        _config;
    private readonly CompanionAutoManager _manager;
    private readonly AutoDesignApplier    _autoDesignApplier;
    private readonly StateManager         _state;
    private readonly ActorObjectManager   _objects;

    public CompanionAutoApplier(Configuration config, CompanionAutoManager manager, AutoDesignApplier autoDesignApplier, StateManager state,
        ActorObjectManager objects)
    {
        _config            = config;
        _manager           = manager;
        _autoDesignApplier = autoDesignApplier;
        _state             = state;
        _objects           = objects;
        _manager.Changed  += OnChanged;
    }

    public void Dispose()
        => _manager.Changed -= OnChanged;

    private void OnChanged()
    {
        if (!_config.EnableAutoDesigns)
            return;

        foreach (var (id, data) in _objects.ToArray())
        {
            if (!_manager.IsLocalPlayerCompanion(id))
                continue;

            if (_manager.TryGetEnabledSet(id, out _))
            {
                if (!_state.GetOrCreate(id, data.Objects[0], out var state))
                    continue;

                _autoDesignApplier.ReapplyAutomation(data.Objects[0], id, state, false, true, out var forcedRedraw);
                foreach (var actor in data.Objects)
                    _state.ReapplyAutomationState(actor, forcedRedraw, false, StateSource.Fixed);
            }
            else if (_state.TryGetValue(id, out var state)
                  && !state.BaseData.IsHuman
                  && state.ModelData.IsHuman
                  && state.Sources[MetaIndex.ModelId] is StateSource.Fixed)
            {
                _state.ResetState(state, StateSource.Manual);
            }
        }
    }
}
