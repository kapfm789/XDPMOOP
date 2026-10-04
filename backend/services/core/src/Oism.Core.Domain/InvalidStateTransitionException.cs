using Oism.SharedKernel;

namespace Oism.Core.Domain;

// Bước chuyển không có trong bảng ở docs/design/state-machines.md.
public sealed class InvalidStateTransitionException(string subject, string from, string to)
    : OismException("invalid_state_transition", $"Không thể chuyển {subject} từ {from} sang {to}", new { from, to });
