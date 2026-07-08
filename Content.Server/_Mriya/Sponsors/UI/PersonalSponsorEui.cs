using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Mriya.Sponsors;
using Robust.Shared.Player;
using System.Linq;
using System.Threading.Tasks;

namespace Content.Server.Mriya.Sponsors.UI;

public sealed partial class PersonalSponsorEui : BaseEui
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ISponsorManager _sponsorManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IEntityManager _entManager = default!;

    private ISawmill _sawmill = default!;

    private bool _isLoading = true;
    private MriyaSponsor? _cachedSponsor;

    public PersonalSponsorEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("sponsors.personal");
    }

    public override void Opened()
    {
        base.Opened();
        LoadDataAsync();
    }

    private async void LoadDataAsync()
    {
        _isLoading = true;
        StateDirty();

        // Завантажуємо спонсора зі всіма його ролями та рангами
        _cachedSponsor = await _db.GetSponsorDataForAsync(Player.UserId);

        _isLoading = false;
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        if (_isLoading || _cachedSponsor == null)
        {
            // Якщо ще вантажимо, або гравець не спонсор взагалі (відправляємо порожні дозволи)
            return new PersonalSponsorSettingsEuiState(
                false, false, null, null, null, null, new List<PersonalSponsorRankInfo>());
        }

        // 1. Вираховуємо глобальні права гравця (чи є хоча б один ранг, що це дозволяє)
        var canSetCustomGhostColor = _cachedSponsor.RoleAssignments.Any(ra => ra.Rank != null && ra.Rank.CanSetGhostColor);
        var canSetCustomOocColor = _cachedSponsor.RoleAssignments.Any(ra => ra.Rank != null && ra.Rank.CanSetOocColor);

        // 2. Формуємо відсортований список доступних рангів для вибору фіксованих кольорів
        var allowedRanks = _cachedSponsor.RoleAssignments
            .Where(ra => ra.Rank != null && ra.Rank.ShowInSponsorWindow)
            // Сортуємо: чим менше число, тим вище пріоритет
            .OrderBy(ra => ra.Rank!.Priority)
            .Select(ra => new PersonalSponsorRankInfo
            {
                Id = ra.Rank!.Id,
                Name = ra.Rank.Name,
                DefaultColor = ra.Rank.DefaultColor,
                FixedGhostColor = ra.Rank.DefaultGhostColor,
                FixedOocColor = ra.Rank.DefaultOocColor
            })
            .ToList();

        // 3. Відправляємо стан на клієнт
        return new PersonalSponsorSettingsEuiState(
            canSetCustomGhostColor,
            canSetCustomOocColor,
            _cachedSponsor.SelectedGhostColor,
            _cachedSponsor.SelectedOocColor,
            _cachedSponsor.SelectedGhostRankId,
            _cachedSponsor.SelectedOocRankId,
            allowedRanks
        );
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is PersonalSponsorEuiMsg.UpdateSettings updateMsg)
        {
            await HandleUpdateSettingsAsync(updateMsg);
        }
    }

    private async Task HandleUpdateSettingsAsync(PersonalSponsorEuiMsg.UpdateSettings msg)
    {
        if (_cachedSponsor == null)
            return;

        bool isModified = false;

        // Очищаємо кольори: видаляємо прозорість і перевіряємо на валідність формату HEX
        var safeGhostColor = StripAlpha(msg.NewGhostColor);
        var safeOocColor = StripAlpha(msg.NewOocColor);

        // --- ПРИВИД ---
        var canSetGhostColor = _cachedSponsor.RoleAssignments.Any(ra => ra.Rank != null && ra.Rank.CanSetGhostColor);

        if (canSetGhostColor)
        {
            if (_cachedSponsor.SelectedGhostColor != safeGhostColor)
            {
                _cachedSponsor.SelectedGhostColor = safeGhostColor;
                isModified = true;
            }
        }
        else if (!string.IsNullOrEmpty(msg.NewGhostColor))
        {
            _sawmill.Warning($"Player {Player.UserId} tried to set a custom ghost color without permission!");
        }

        if (msg.SelectedGhostRankId != _cachedSponsor.SelectedGhostRankId)
        {
            if (msg.SelectedGhostRankId == null || _cachedSponsor.RoleAssignments.Any(ra => ra.RankId == msg.SelectedGhostRankId))
            {
                _cachedSponsor.SelectedGhostRankId = msg.SelectedGhostRankId;
                isModified = true;
            }
            else
            {
                _sawmill.Warning($"Player {Player.UserId} tried to select an unowned rank ({msg.SelectedGhostRankId}) for ghost color!");
            }
        }

        // --- ООС ---
        var canSetOocColor = _cachedSponsor.RoleAssignments.Any(ra => ra.Rank != null && ra.Rank.CanSetOocColor);

        if (canSetOocColor)
        {
            if (_cachedSponsor.SelectedOocColor != safeOocColor)
            {
                _cachedSponsor.SelectedOocColor = safeOocColor;
                isModified = true;
            }
        }
        else if (!string.IsNullOrEmpty(msg.NewOocColor))
        {
            _sawmill.Warning($"Player {Player.UserId} tried to set a custom OOC color without permission!");
        }

        if (msg.SelectedOocRankId != _cachedSponsor.SelectedOocRankId)
        {
            if (msg.SelectedOocRankId == null || _cachedSponsor.RoleAssignments.Any(ra => ra.RankId == msg.SelectedOocRankId))
            {
                _cachedSponsor.SelectedOocRankId = msg.SelectedOocRankId;
                isModified = true;
            }
            else
            {
                _sawmill.Warning($"Player {Player.UserId} tried to select an unowned rank ({msg.SelectedOocRankId}) for OOC color!");
            }
        }

        // --- ЗБЕРЕЖЕННЯ ---
        if (isModified)
        {
            await _db.UpdateSponsorAsync(_cachedSponsor);
            _sponsorManager.UpdateCache(Player.UserId, _cachedSponsor);
            _sawmill.Info($"Player {Player.UserId} successfully updated their personal sponsor settings.");
            _entManager.EventBus.RaiseEvent(EventSource.Local, new SaveGhostColorEvent(Player));
            StateDirty();
        }
    }

    private string? StripAlpha(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (Color.TryParse(input, out var color))
        {
            return $"#{color.RByte:X2}{color.GByte:X2}{color.BByte:X2}";
        }
        return null;
    }
}

public record struct SaveGhostColorEvent(ICommonSession Session);
