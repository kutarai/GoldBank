using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Ekub.Domain.Entities;
using GoldBank.Core.Modules.Customers.Domain.Entities;
using GoldBank.Protos.Ekub;
using GoldBank.SharedKernel.MultiTenancy;

namespace GoldBank.Core.Modules.Ekub.Grpc;

/// <summary>
/// gRPC service for the Ekub module — group savings and (in v2) lending.
/// All RPCs are person-scoped: every request carries the acting customer_id and
/// authorisation is by EkubMembership.Role within the target group.
/// </summary>
public sealed class EkubGrpcService : EkubService.EkubServiceBase
{
    private const int InvitationTtlDays = 30;
    private const int MinActiveMembers = 3;

    private readonly GoldBankDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<EkubGrpcService> _logger;

    public EkubGrpcService(
        GoldBankDbContext db,
        ITenantProvider tenantProvider,
        ILogger<EkubGrpcService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // =========================================================================
    // Group lifecycle
    // =========================================================================

    public override async Task<GroupResponse> CreateGroup(CreateGroupRequest request, ServerCallContext context)
    {
        var customerId = ParseGuid(request.CustomerId, "customer_id");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required."));

        var currency = (request.Currency ?? "").ToUpperInvariant();
        if (currency is not "ZWG" and not "USD")
            throw new RpcException(new Status(StatusCode.InvalidArgument, "currency must be ZWG or USD."));

        if (!decimal.TryParse(request.MonthlyContribution, NumberStyles.Number, CultureInfo.InvariantCulture, out var monthly) || monthly <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "monthly_contribution must be a positive number."));

        var rate = 0m;
        if (!string.IsNullOrWhiteSpace(request.LoanInterestRatePercent))
        {
            if (!decimal.TryParse(request.LoanInterestRatePercent, NumberStyles.Number, CultureInfo.InvariantCulture, out rate) || rate < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "loan_interest_rate_percent must be non-negative."));
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.DeletedAt == null, context.CancellationToken);
        if (customer is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Customer not found."));

        var now = DateTime.UtcNow;
        var group = new EkubGroup
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Currency = currency,
            MonthlyContribution = monthly,
            LoanInterestRatePercent = rate,
            ApplyInterestOnContributions = request.ApplyInterestOnContributions,
            Status = EkubGroupStatus.Forming,
            ChairmanCustomerId = customerId,
            TenantId = customer.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.EkubGroups.Add(group);

        var chairmanship = new EkubMembership
        {
            GroupId = group.Id,
            CustomerId = customerId,
            Role = EkubMemberRole.Chairman,
            JoinedAt = now,
            TenantId = customer.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.EkubMemberships.Add(chairmanship);

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Ekub group created: {GroupId} '{Name}' by chairman {ChairmanId}",
            group.Id, group.Name, customerId);

        return await MapGroupResponseAsync(group, context.CancellationToken);
    }

    public override async Task<GroupResponse> AssignRole(AssignRoleRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");
        var targetId = ParseGuid(request.TargetCustomerId, "target_customer_id");

        if (!System.Enum.TryParse<EkubMemberRole>(request.Role, ignoreCase: true, out var newRole))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "role must be Chairman, Treasurer, Secretary or Member."));

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        if (group.ChairmanCustomerId != requesterId)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the chairman may assign roles."));

        var members = await _db.EkubMemberships
            .Where(m => m.GroupId == groupId && m.LeftAt == null)
            .ToListAsync(context.CancellationToken);

        var target = members.FirstOrDefault(m => m.CustomerId == targetId)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Target customer is not an active member of this group."));

        // Roles other than Member are unique within the group — demote anyone else holding the role.
        if (newRole is not EkubMemberRole.Member)
        {
            foreach (var m in members.Where(m => m.Role == newRole && m.Id != target.Id))
                m.Role = EkubMemberRole.Member;
        }

        if (newRole == EkubMemberRole.Chairman)
            group.ChairmanCustomerId = targetId;

        target.Role = newRole;
        target.UpdatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Ekub role assigned: group {GroupId} customer {Target} → {Role}", groupId, targetId, newRole);

        return await MapGroupResponseAsync(group, context.CancellationToken);
    }

    public override async Task<GoldBank.Protos.Common.StatusResponse> CloseGroup(CloseGroupRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        if (group.ChairmanCustomerId != requesterId)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the chairman may close the group."));

        group.Status = EkubGroupStatus.Closed;
        group.ClosedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);

        return new GoldBank.Protos.Common.StatusResponse { Success = true, Message = "Group closed." };
    }

    // =========================================================================
    // Membership
    // =========================================================================

    public override async Task<InvitationResponse> InviteMember(InviteMemberRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var inviterId = ParseGuid(request.InviterCustomerId, "inviter_customer_id");

        if (string.IsNullOrWhiteSpace(request.InviteePhone))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invitee_phone is required."));

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        if (group.Status is EkubGroupStatus.Closed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Group is closed."));

        var inviter = await _db.EkubMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.CustomerId == inviterId && m.LeftAt == null,
                context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Inviter is not an active member."));

        if (inviter.Role is not (EkubMemberRole.Chairman or EkubMemberRole.Secretary))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only chairman or secretary may invite members."));

        var phone = request.InviteePhone.Trim();

        // Don't invite someone who's already a member.
        var inviteeCustomer = await _db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone && c.DeletedAt == null, context.CancellationToken);
        if (inviteeCustomer is not null)
        {
            var existing = await _db.EkubMemberships.AnyAsync(
                m => m.GroupId == groupId && m.CustomerId == inviteeCustomer.Id && m.LeftAt == null,
                context.CancellationToken);
            if (existing)
                throw new RpcException(new Status(StatusCode.AlreadyExists, "That person is already a member of this group."));
        }

        var pending = await _db.EkubInvitations.FirstOrDefaultAsync(
            i => i.GroupId == groupId && i.InviteePhone == phone && i.Status == EkubInvitationStatus.Pending,
            context.CancellationToken);
        if (pending is not null)
            throw new RpcException(new Status(StatusCode.AlreadyExists, "An invitation is already pending for that phone."));

        var now = DateTime.UtcNow;
        var invitation = new EkubInvitation
        {
            GroupId = groupId,
            InviteePhone = phone,
            InviteeCustomerId = inviteeCustomer?.Id,
            InviterCustomerId = inviterId,
            Status = EkubInvitationStatus.Pending,
            ExpiresAt = now.AddDays(InvitationTtlDays),
            TenantId = group.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.EkubInvitations.Add(invitation);
        await _db.SaveChangesAsync(context.CancellationToken);

        return MapInvitationResponse(invitation, group.Name);
    }

    public override async Task<GoldBank.Protos.Common.StatusResponse> RevokeInvitation(RevokeInvitationRequest request, ServerCallContext context)
    {
        var invitationId = ParseGuid(request.InvitationId, "invitation_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");

        var invitation = await _db.EkubInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Invitation not found."));

        if (invitation.Status != EkubInvitationStatus.Pending)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Invitation is no longer pending."));

        var requester = await _db.EkubMemberships.FirstOrDefaultAsync(
            m => m.GroupId == invitation.GroupId && m.CustomerId == requesterId && m.LeftAt == null,
            context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Requester is not a member."));
        if (requester.Role is not (EkubMemberRole.Chairman or EkubMemberRole.Secretary))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only chairman or secretary may revoke invitations."));

        invitation.Status = EkubInvitationStatus.Revoked;
        invitation.RespondedAt = DateTime.UtcNow;
        invitation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);

        return new GoldBank.Protos.Common.StatusResponse { Success = true, Message = "Invitation revoked." };
    }

    public override async Task<ListInvitationsResponse> ListMyInvitations(ListMyInvitationsRequest request, ServerCallContext context)
    {
        var customerId = ParseGuid(request.CustomerId, "customer_id");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer not found."));

        var now = DateTime.UtcNow;

        // Match by phone (covers invitations created before they registered) AND by customer_id.
        var rows = await _db.EkubInvitations
            .Where(i => (i.InviteeCustomerId == customerId || i.InviteePhone == customer.PhoneNumber)
                        && i.Status == EkubInvitationStatus.Pending
                        && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var groupIds = rows.Select(r => r.GroupId).ToHashSet();
        var groups = await _db.EkubGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, context.CancellationToken);

        var resp = new ListInvitationsResponse();
        foreach (var inv in rows)
        {
            var name = groups.TryGetValue(inv.GroupId, out var g) ? g.Name : "";
            resp.Invitations.Add(MapInvitationResponse(inv, name));
        }
        return resp;
    }

    public override async Task<GoldBank.Protos.Common.StatusResponse> RespondToInvitation(RespondToInvitationRequest request, ServerCallContext context)
    {
        var invitationId = ParseGuid(request.InvitationId, "invitation_id");
        var customerId = ParseGuid(request.CustomerId, "customer_id");

        var invitation = await _db.EkubInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Invitation not found."));

        if (invitation.Status != EkubInvitationStatus.Pending)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Invitation is no longer pending."));

        if (invitation.ExpiresAt <= DateTime.UtcNow)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Invitation has expired."));

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer not found."));

        if (invitation.InviteePhone != customer.PhoneNumber &&
            invitation.InviteeCustomerId != customerId)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Invitation is not for this customer."));

        var now = DateTime.UtcNow;
        invitation.RespondedAt = now;
        invitation.InviteeCustomerId = customerId;
        invitation.UpdatedAt = now;

        if (!request.Accept)
        {
            invitation.Status = EkubInvitationStatus.Declined;
            await _db.SaveChangesAsync(context.CancellationToken);
            return new GoldBank.Protos.Common.StatusResponse { Success = true, Message = "Invitation declined." };
        }

        // Accept: create membership, possibly activate group.
        var group = await LoadGroupAsync(invitation.GroupId, context.CancellationToken);
        if (group.Status is EkubGroupStatus.Closed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Group is closed."));

        var alreadyMember = await _db.EkubMemberships.AnyAsync(
            m => m.GroupId == group.Id && m.CustomerId == customerId && m.LeftAt == null,
            context.CancellationToken);
        if (alreadyMember)
            throw new RpcException(new Status(StatusCode.AlreadyExists, "Already a member of this group."));

        _db.EkubMemberships.Add(new EkubMembership
        {
            GroupId = group.Id,
            CustomerId = customerId,
            Role = EkubMemberRole.Member,
            JoinedAt = now,
            TenantId = group.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        invitation.Status = EkubInvitationStatus.Accepted;

        await _db.SaveChangesAsync(context.CancellationToken);

        // Activate when quorum reached.
        var activeMembers = await _db.EkubMemberships
            .CountAsync(m => m.GroupId == group.Id && m.LeftAt == null, context.CancellationToken);
        if (group.Status == EkubGroupStatus.Forming && activeMembers >= MinActiveMembers)
        {
            group.Status = EkubGroupStatus.Active;
            group.ActivatedAt = now;
            group.UpdatedAt = now;
            await _db.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Ekub group activated: {GroupId} '{Name}' ({Members} members)", group.Id, group.Name, activeMembers);
        }

        return new GoldBank.Protos.Common.StatusResponse { Success = true, Message = "Invitation accepted; you are now a member." };
    }

    public override async Task<GoldBank.Protos.Common.StatusResponse> KickMember(KickMemberRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");
        var targetId = ParseGuid(request.TargetCustomerId, "target_customer_id");

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        if (group.ChairmanCustomerId != requesterId)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the chairman may remove members."));
        if (targetId == group.ChairmanCustomerId)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Chairman cannot remove themselves; transfer the role first."));

        var membership = await _db.EkubMemberships.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.CustomerId == targetId && m.LeftAt == null,
            context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Target is not an active member."));

        var now = DateTime.UtcNow;
        membership.LeftAt = now;
        membership.ExitReason = string.IsNullOrWhiteSpace(request.Reason) ? "Removed by chairman" : request.Reason.Trim();
        membership.UpdatedAt = now;

        await _db.SaveChangesAsync(context.CancellationToken);

        // v1: pro-rata refund is recorded as an EkubFee placeholder; v3 will turn this into a real cash-out via teller.
        // Intentionally noop here so we don't double-count; the member's contributions remain in the pot until a refund is issued.

        return new GoldBank.Protos.Common.StatusResponse { Success = true, Message = "Member removed; pro-rata refund pending." };
    }

    // =========================================================================
    // Contributions
    // =========================================================================

    public override async Task<ContributionResponse> RecordContribution(RecordContributionRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var customerId = ParseGuid(request.CustomerId, "customer_id");

        if (!decimal.TryParse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "amount must be a positive number."));

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        if (group.Status != EkubGroupStatus.Active)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Group is not active."));

        var membership = await _db.EkubMemberships.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.CustomerId == customerId && m.LeftAt == null,
            context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Caller is not an active member."));

        var period = string.IsNullOrWhiteSpace(request.Period)
            ? DateTime.UtcNow.ToString("yyyy-MM")
            : request.Period.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(period, @"^\d{4}-\d{2}$"))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "period must be in 'YYYY-MM' format."));

        var now = DateTime.UtcNow;
        var contribution = new EkubContribution
        {
            GroupId = groupId,
            CustomerId = customerId,
            MembershipId = membership.Id,
            Amount = amount,
            Currency = group.Currency,
            Period = period,
            Status = EkubContributionStatus.Pending,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            TenantId = group.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.EkubContributions.Add(contribution);
        await _db.SaveChangesAsync(context.CancellationToken);

        return MapContributionResponse(contribution);
    }

    public override async Task<ContributionResponse> ConfirmContribution(ConfirmContributionRequest request, ServerCallContext context)
    {
        var contributionId = ParseGuid(request.ContributionId, "contribution_id");
        var treasurerId = ParseGuid(request.TreasurerCustomerId, "treasurer_customer_id");

        var contribution = await _db.EkubContributions.FirstOrDefaultAsync(c => c.Id == contributionId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Contribution not found."));

        if (contribution.Status != EkubContributionStatus.Pending)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Contribution has already been resolved."));

        var treasurer = await _db.EkubMemberships.FirstOrDefaultAsync(
            m => m.GroupId == contribution.GroupId && m.CustomerId == treasurerId && m.LeftAt == null,
            context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Caller is not an active member."));
        if (treasurer.Role != EkubMemberRole.Treasurer)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the treasurer may confirm contributions."));

        var now = DateTime.UtcNow;
        contribution.Status = request.Approve ? EkubContributionStatus.Confirmed : EkubContributionStatus.Rejected;
        contribution.ConfirmedByCustomerId = treasurerId;
        contribution.ConfirmedAt = now;
        contribution.Notes = string.IsNullOrWhiteSpace(request.Notes) ? contribution.Notes : request.Notes.Trim();
        contribution.UpdatedAt = now;

        await _db.SaveChangesAsync(context.CancellationToken);
        return MapContributionResponse(contribution);
    }

    public override async Task<ListContributionsResponse> ListGroupContributions(ListGroupContributionsRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");

        await EnsureMemberAsync(groupId, requesterId, context.CancellationToken);

        var query = _db.EkubContributions.Where(c => c.GroupId == groupId);
        if (!string.IsNullOrWhiteSpace(request.Period))
            query = query.Where(c => c.Period == request.Period);
        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            System.Enum.TryParse<EkubContributionStatus>(request.StatusFilter, ignoreCase: true, out var s))
            query = query.Where(c => c.Status == s);

        var rows = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(context.CancellationToken);

        var resp = new ListContributionsResponse();
        foreach (var c in rows) resp.Contributions.Add(MapContributionResponse(c));
        return resp;
    }

    // =========================================================================
    // Reads
    // =========================================================================

    public override async Task<ListGroupsResponse> ListMyGroups(ListMyGroupsRequest request, ServerCallContext context)
    {
        var customerId = ParseGuid(request.CustomerId, "customer_id");

        var groupIds = await _db.EkubMemberships
            .Where(m => m.CustomerId == customerId && m.LeftAt == null)
            .Select(m => m.GroupId)
            .ToListAsync(context.CancellationToken);

        var query = _db.EkubGroups.Where(g => groupIds.Contains(g.Id));
        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            System.Enum.TryParse<EkubGroupStatus>(request.StatusFilter, ignoreCase: true, out var s))
            query = query.Where(g => g.Status == s);

        var groups = await query.OrderByDescending(g => g.CreatedAt).ToListAsync(context.CancellationToken);

        var resp = new ListGroupsResponse();
        foreach (var g in groups) resp.Groups.Add(await MapGroupResponseAsync(g, context.CancellationToken));
        return resp;
    }

    public override async Task<GroupDetailResponse> GetGroupDetail(GetGroupDetailRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");

        await EnsureMemberAsync(groupId, requesterId, context.CancellationToken);

        var group = await LoadGroupAsync(groupId, context.CancellationToken);

        var memberships = await _db.EkubMemberships
            .Where(m => m.GroupId == groupId)
            .ToListAsync(context.CancellationToken);

        var customerIds = memberships.Select(m => m.CustomerId).ToHashSet();
        var customers = await _db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, context.CancellationToken);

        var detail = new GroupDetailResponse
        {
            Group = await MapGroupResponseAsync(group, context.CancellationToken),
            PotBalance = await ComputePotBalanceAsync(groupId, group.Currency, context.CancellationToken),
        };

        foreach (var m in memberships.OrderBy(m => m.Role).ThenBy(m => m.JoinedAt))
        {
            customers.TryGetValue(m.CustomerId, out var c);
            detail.Members.Add(new MemberResponse
            {
                CustomerId = m.CustomerId.ToString(),
                MembershipId = m.Id.ToString(),
                Role = m.Role.ToString(),
                FirstName = c?.FirstName ?? "",
                LastName = c?.LastName ?? "",
                Phone = c?.PhoneNumber ?? "",
                JoinedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(m.JoinedAt, DateTimeKind.Utc)),
                LeftAt = m.LeftAt.HasValue
                    ? Timestamp.FromDateTime(DateTime.SpecifyKind(m.LeftAt.Value, DateTimeKind.Utc))
                    : null,
            });
        }

        return detail;
    }

    public override async Task<MyShareResponse> GetMyShare(GetMyShareRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var customerId = ParseGuid(request.CustomerId, "customer_id");

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        await EnsureMemberAsync(groupId, customerId, context.CancellationToken);

        var confirmed = await _db.EkubContributions
            .Where(c => c.GroupId == groupId && c.Status == EkubContributionStatus.Confirmed)
            .GroupBy(c => c.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(context.CancellationToken);

        var totalConfirmed = confirmed.Sum(x => x.Total);
        var mine = confirmed.FirstOrDefault(x => x.CustomerId == customerId)?.Total ?? 0m;

        // Interest earnings: distribute the group's accumulated repayment interest pro-rata
        // by confirmed contributions. (Members who joined later still share in proportion to
        // what they put in — same rule as the pot itself.)
        var totalInterest = await _db.EkubLoanRepayments
            .Where(r => r.GroupId == groupId)
            .SumAsync(r => (decimal?)r.InterestPortion, context.CancellationToken) ?? 0m;
        var interestEarnings = totalConfirmed > 0
            ? totalInterest * (mine / totalConfirmed)
            : 0m;

        return new MyShareResponse
        {
            CustomerId = customerId.ToString(),
            GroupId = groupId.ToString(),
            MyContributions = Money(mine, group.Currency),
            MyInterestEarnings = Money(interestEarnings, group.Currency),
            MyShareTotal = Money(mine + interestEarnings, group.Currency),
            MySharePercent = totalConfirmed > 0 ? (mine / totalConfirmed * 100m).ToString("F2", CultureInfo.InvariantCulture) : "0.00",
        };
    }

    // =========================================================================
    // Loans (v2)
    // =========================================================================

    public override async Task<LoanResponse> ApplyForLoan(ApplyForLoanRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var borrowerId = ParseGuid(request.BorrowerCustomerId, "borrower_customer_id");

        if (!decimal.TryParse(request.Principal, NumberStyles.Number, CultureInfo.InvariantCulture, out var principal) || principal <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "principal must be positive."));
        if (request.TermMonths <= 0 || request.TermMonths > 60)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "term_months must be 1..60."));

        var group = await LoadGroupAsync(groupId, context.CancellationToken);
        if (group.Status != EkubGroupStatus.Active)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Group is not active."));

        await EnsureMemberAsync(groupId, borrowerId, context.CancellationToken);

        // Don't allow stacking — one open loan per borrower at a time.
        var openLoanStatuses = new[] { EkubLoanStatus.Voting, EkubLoanStatus.AwaitingTreasurer, EkubLoanStatus.Disbursed, EkubLoanStatus.Repaying };
        var hasOpenLoan = await _db.EkubLoans.AnyAsync(
            l => l.GroupId == groupId && l.BorrowerCustomerId == borrowerId && openLoanStatuses.Contains(l.Status),
            context.CancellationToken);
        if (hasOpenLoan)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Borrower already has an open loan in this group."));

        // Pot must cover the principal at the time of application — but we also
        // need to net off any in-flight applications (Voting / AwaitingTreasurer)
        // because their principals haven't yet hit the disbursed ledger. Without
        // this, two members applying for amounts that individually fit but
        // together overflow the pot would both pass, then deficit at confirm time.
        var pot = await ComputePotBalanceRawAsync(groupId, context.CancellationToken);
        var pendingPrincipal = await _db.EkubLoans
            .Where(l => l.GroupId == groupId &&
                        (l.Status == EkubLoanStatus.Voting ||
                         l.Status == EkubLoanStatus.AwaitingTreasurer))
            .SumAsync(l => (decimal?)l.Principal, context.CancellationToken) ?? 0m;
        var available = pot - pendingPrincipal;
        if (available < principal)
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"Insufficient pot balance. Available: {available:F2} " +
                $"(pot {pot:F2} − pending loans {pendingPrincipal:F2}), requested: {principal:F2}."));

        var rate = group.LoanInterestRatePercent;

        // Some groups don't charge interest on the portion of a loan that is covered
        // by the borrower's own confirmed contributions — they're effectively borrowing
        // their own money. Only the excess accrues interest. When the flag is on
        // (default), interest applies to the full principal as before.
        decimal interestableAmount;
        if (!group.ApplyInterestOnContributions)
        {
            var ownContributions = await _db.EkubContributions
                .Where(c => c.GroupId == groupId
                            && c.CustomerId == borrowerId
                            && c.Status == EkubContributionStatus.Confirmed)
                .SumAsync(c => (decimal?)c.Amount, context.CancellationToken) ?? 0m;
            interestableAmount = principal > ownContributions ? principal - ownContributions : 0m;
        }
        else
        {
            interestableAmount = principal;
        }

        var totalInterest  = interestableAmount * (rate / 100m) * request.TermMonths / 12m;
        var totalRepayable = principal + totalInterest;
        var installment    = totalRepayable / request.TermMonths;
        var now            = DateTime.UtcNow;

        var loan = new EkubLoan
        {
            GroupId = groupId,
            BorrowerCustomerId = borrowerId,
            Principal = principal,
            InterestRatePercent = rate,
            TermMonths = request.TermMonths,
            TotalRepayable = totalRepayable,
            InstallmentAmount = installment,
            OutstandingBalance = totalRepayable,
            TotalInterestEarned = 0m,
            Currency = group.Currency,
            Status = EkubLoanStatus.Voting,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(),
            TenantId = group.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.EkubLoans.Add(loan);
        await _db.SaveChangesAsync(context.CancellationToken);

        return await MapLoanResponseAsync(loan, context.CancellationToken);
    }

    public override async Task<LoanResponse> VoteOnLoan(VoteOnLoanRequest request, ServerCallContext context)
    {
        var loanId = ParseGuid(request.LoanId, "loan_id");
        var voterId = ParseGuid(request.VoterCustomerId, "voter_customer_id");

        var loan = await _db.EkubLoans.FirstOrDefaultAsync(l => l.Id == loanId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Loan not found."));
        if (loan.Status != EkubLoanStatus.Voting)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Loan is not in voting state."));
        if (loan.BorrowerCustomerId == voterId)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Borrower cannot vote on their own loan."));

        await EnsureMemberAsync(loan.GroupId, voterId, context.CancellationToken);

        var existing = await _db.EkubLoanVotes
            .FirstOrDefaultAsync(v => v.LoanId == loanId && v.VoterCustomerId == voterId, context.CancellationToken);
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            _db.EkubLoanVotes.Add(new EkubLoanVote
            {
                LoanId = loanId,
                VoterCustomerId = voterId,
                Approve = request.Approve,
                TenantId = loan.TenantId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Approve = request.Approve;
            existing.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(context.CancellationToken);

        // Resolution: >50% of (active members − borrower) approve → AwaitingTreasurer.
        // Equivalently, if approves > rejects+remaining → already won; if rejects ≥ majority threshold → Rejected.
        var totalEligible = await _db.EkubMemberships.CountAsync(
            m => m.GroupId == loan.GroupId && m.LeftAt == null && m.CustomerId != loan.BorrowerCustomerId,
            context.CancellationToken);
        var approves = await _db.EkubLoanVotes.CountAsync(v => v.LoanId == loanId && v.Approve, context.CancellationToken);
        var rejects = await _db.EkubLoanVotes.CountAsync(v => v.LoanId == loanId && !v.Approve, context.CancellationToken);
        var threshold = totalEligible / 2 + 1; // strict majority

        if (approves >= threshold)
        {
            loan.Status = EkubLoanStatus.AwaitingTreasurer;
            loan.UpdatedAt = now;
        }
        else if (rejects > totalEligible - threshold)
        {
            loan.Status = EkubLoanStatus.Rejected;
            loan.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Ekub loan vote: loan {LoanId} voter {Voter} approve={Approve} → {Approves}/{Eligible}",
            loanId, voterId, request.Approve, approves, totalEligible);

        return await MapLoanResponseAsync(loan, context.CancellationToken, voterId);
    }

    public override async Task<LoanResponse> ConfirmLoanByTreasurer(ConfirmLoanRequest request, ServerCallContext context)
    {
        var loanId = ParseGuid(request.LoanId, "loan_id");
        var treasurerId = ParseGuid(request.TreasurerCustomerId, "treasurer_customer_id");

        var loan = await _db.EkubLoans.FirstOrDefaultAsync(l => l.Id == loanId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Loan not found."));
        if (loan.Status != EkubLoanStatus.AwaitingTreasurer)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Loan is not awaiting treasurer confirmation."));

        var treasurer = await _db.EkubMemberships.FirstOrDefaultAsync(
            m => m.GroupId == loan.GroupId && m.CustomerId == treasurerId && m.LeftAt == null,
            context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Caller is not an active member."));
        if (treasurer.Role != EkubMemberRole.Treasurer)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the treasurer may confirm loans."));

        var now = DateTime.UtcNow;
        loan.TreasurerCustomerId = treasurerId;
        loan.Notes = string.IsNullOrWhiteSpace(request.Notes) ? loan.Notes : request.Notes.Trim();
        loan.UpdatedAt = now;
        if (request.Approve)
        {
            // Defence in depth — re-check the pot at confirm time. Even though
            // ApplyForLoan validates against pot − pending loans, the pot can
            // change between application and confirmation (fees applied, other
            // loans disbursed, etc.). Refusing here is safer than disbursing
            // into a deficit.
            var pot = await ComputePotBalanceRawAsync(loan.GroupId, context.CancellationToken);
            if (pot < loan.Principal)
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    $"Insufficient pot to disburse. Available: {pot:F2}, loan: {loan.Principal:F2}."));

            loan.Status = EkubLoanStatus.Disbursed;
            loan.DisbursedAt = now;
        }
        else
        {
            loan.Status = EkubLoanStatus.Rejected;
        }
        await _db.SaveChangesAsync(context.CancellationToken);

        return await MapLoanResponseAsync(loan, context.CancellationToken, treasurerId);
    }

    public override async Task<LoanResponse> RecordLoanRepayment(RecordLoanRepaymentRequest request, ServerCallContext context)
    {
        var loanId = ParseGuid(request.LoanId, "loan_id");
        var treasurerId = ParseGuid(request.TreasurerCustomerId, "treasurer_customer_id");

        if (!decimal.TryParse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "amount must be positive."));

        var loan = await _db.EkubLoans.FirstOrDefaultAsync(l => l.Id == loanId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Loan not found."));
        if (loan.Status is not (EkubLoanStatus.Disbursed or EkubLoanStatus.Repaying))
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Loan is not in a repayable state."));

        var treasurer = await _db.EkubMemberships.FirstOrDefaultAsync(
            m => m.GroupId == loan.GroupId && m.CustomerId == treasurerId && m.LeftAt == null,
            context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Caller is not an active member."));
        if (treasurer.Role != EkubMemberRole.Treasurer)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the treasurer may record repayments."));

        if (amount > loan.OutstandingBalance)
            amount = loan.OutstandingBalance; // clamp the final payment

        // Split principal vs interest in proportion to the original ratio. Total interest on the loan
        // is (TotalRepayable − Principal); allocate this payment's share by the same ratio.
        var totalInterest = loan.TotalRepayable - loan.Principal;
        var interestPortion = loan.TotalRepayable > 0
            ? amount * (totalInterest / loan.TotalRepayable)
            : 0m;
        var principalPortion = amount - interestPortion;

        var now = DateTime.UtcNow;
        _db.EkubLoanRepayments.Add(new EkubLoanRepayment
        {
            LoanId = loanId,
            GroupId = loan.GroupId,
            TreasurerCustomerId = treasurerId,
            AmountPaid = amount,
            PrincipalPortion = principalPortion,
            InterestPortion = interestPortion,
            Currency = loan.Currency,
            TenantId = loan.TenantId,
            CreatedAt = now,
            UpdatedAt = now,
        });

        loan.OutstandingBalance -= amount;
        loan.TotalInterestEarned += interestPortion;
        loan.Status = loan.OutstandingBalance <= 0m ? EkubLoanStatus.Closed : EkubLoanStatus.Repaying;
        if (loan.Status == EkubLoanStatus.Closed) loan.ClosedAt = now;
        loan.UpdatedAt = now;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Ekub repayment: loan {LoanId} {Amount} (principal={Principal}, interest={Interest}); outstanding {Outstanding}",
            loanId, amount, principalPortion, interestPortion, loan.OutstandingBalance);

        return await MapLoanResponseAsync(loan, context.CancellationToken);
    }

    public override async Task<ListLoansResponse> ListGroupLoans(ListGroupLoansRequest request, ServerCallContext context)
    {
        var groupId = ParseGuid(request.GroupId, "group_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");
        await EnsureMemberAsync(groupId, requesterId, context.CancellationToken);

        var query = _db.EkubLoans.Where(l => l.GroupId == groupId);
        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            System.Enum.TryParse<EkubLoanStatus>(request.StatusFilter, ignoreCase: true, out var s))
            query = query.Where(l => l.Status == s);

        var loans = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(context.CancellationToken);
        var resp = new ListLoansResponse();
        foreach (var l in loans) resp.Loans.Add(await MapLoanResponseAsync(l, context.CancellationToken, requesterId));
        return resp;
    }

    public override async Task<ListLoansResponse> ListMyLoans(ListMyLoansRequest request, ServerCallContext context)
    {
        var customerId = ParseGuid(request.CustomerId, "customer_id");
        var loans = await _db.EkubLoans
            .Where(l => l.BorrowerCustomerId == customerId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(context.CancellationToken);

        // Borrower-on-own-loans never has a vote (borrower can't vote on themselves);
        // pass requester anyway so the field is consistent.
        var resp = new ListLoansResponse();
        foreach (var l in loans) resp.Loans.Add(await MapLoanResponseAsync(l, context.CancellationToken, customerId));
        return resp;
    }

    public override async Task<LoanDetailResponse> GetLoanDetail(GetLoanDetailRequest request, ServerCallContext context)
    {
        var loanId = ParseGuid(request.LoanId, "loan_id");
        var requesterId = ParseGuid(request.RequesterCustomerId, "requester_customer_id");

        var loan = await _db.EkubLoans.FirstOrDefaultAsync(l => l.Id == loanId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Loan not found."));
        await EnsureMemberAsync(loan.GroupId, requesterId, context.CancellationToken);

        var votes = await _db.EkubLoanVotes
            .Where(v => v.LoanId == loanId)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var totalRepaid = await _db.EkubLoanRepayments
            .Where(r => r.LoanId == loanId)
            .SumAsync(r => (decimal?)r.AmountPaid, context.CancellationToken) ?? 0m;

        var resp = new LoanDetailResponse
        {
            Loan = await MapLoanResponseAsync(loan, context.CancellationToken, requesterId),
            TotalRepaid = Money(totalRepaid, loan.Currency),
        };
        foreach (var v in votes)
        {
            resp.Votes.Add(new VoteResponse
            {
                VoterCustomerId = v.VoterCustomerId.ToString(),
                Approve = v.Approve,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(v.CreatedAt, DateTimeKind.Utc)),
            });
        }
        return resp;
    }

    private async Task<decimal> ComputePotBalanceRawAsync(Guid groupId, CancellationToken ct)
    {
        var contributions = await _db.EkubContributions
            .Where(c => c.GroupId == groupId && c.Status == EkubContributionStatus.Confirmed)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var fees = await _db.EkubFees
            .Where(f => f.GroupId == groupId)
            .SumAsync(f => (decimal?)f.Amount, ct) ?? 0m;
        var disbursed = await _db.EkubLoans
            .Where(l => l.GroupId == groupId && (l.Status == EkubLoanStatus.Disbursed || l.Status == EkubLoanStatus.Repaying || l.Status == EkubLoanStatus.Closed || l.Status == EkubLoanStatus.Defaulted))
            .SumAsync(l => (decimal?)l.Principal, ct) ?? 0m;
        var repaid = await _db.EkubLoanRepayments
            .Where(r => r.GroupId == groupId)
            .SumAsync(r => (decimal?)r.AmountPaid, ct) ?? 0m;
        return contributions - fees - disbursed + repaid;
    }

    private async Task<LoanResponse> MapLoanResponseAsync(EkubLoan loan, CancellationToken ct, Guid? requesterId = null)
    {
        var approves = await _db.EkubLoanVotes.CountAsync(v => v.LoanId == loan.Id && v.Approve, ct);
        var rejects = await _db.EkubLoanVotes.CountAsync(v => v.LoanId == loan.Id && !v.Approve, ct);
        var totalEligible = await _db.EkubMemberships.CountAsync(
            m => m.GroupId == loan.GroupId && m.LeftAt == null && m.CustomerId != loan.BorrowerCustomerId, ct);

        var myVote = "";
        if (requesterId.HasValue)
        {
            var v = await _db.EkubLoanVotes
                .Where(x => x.LoanId == loan.Id && x.VoterCustomerId == requesterId.Value)
                .Select(x => (bool?)x.Approve)
                .FirstOrDefaultAsync(ct);
            myVote = v switch { true => "Approve", false => "Reject", _ => "" };
        }

        var resp = new LoanResponse
        {
            Id = loan.Id.ToString(),
            GroupId = loan.GroupId.ToString(),
            BorrowerCustomerId = loan.BorrowerCustomerId.ToString(),
            Principal = Money(loan.Principal, loan.Currency),
            InterestRatePercent = loan.InterestRatePercent.ToString("F3", CultureInfo.InvariantCulture),
            TermMonths = loan.TermMonths,
            InstallmentAmount = Money(loan.InstallmentAmount, loan.Currency),
            OutstandingBalance = Money(loan.OutstandingBalance, loan.Currency),
            Status = loan.Status.ToString(),
            ApprovedVotes = approves,
            RejectedVotes = rejects,
            TotalEligibleVoters = totalEligible,
            MyVote = myVote,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(loan.CreatedAt, DateTimeKind.Utc)),
        };
        if (loan.DisbursedAt.HasValue)
            resp.DisbursedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(loan.DisbursedAt.Value, DateTimeKind.Utc));
        return resp;
    }

    // =========================================================================
    // Monthly fee
    // =========================================================================

    public override async Task<ApplyMonthlyFeeResponse> ApplyMonthlyFee(ApplyMonthlyFeeRequest request, ServerCallContext context)
    {
        var period = string.IsNullOrWhiteSpace(request.Period)
            ? DateTime.UtcNow.ToString("yyyy-MM")
            : request.Period.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(period, @"^\d{4}-\d{2}$"))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "period must be in 'YYYY-MM' format."));

        var configs = await _db.SystemConfigs
            .Where(c => c.Key == "ekub.monthly_fee_zwg" || c.Key == "ekub.monthly_fee_usd")
            .ToDictionaryAsync(c => c.Key, c => c.ValueJson, context.CancellationToken);

        decimal feeFor(string currency)
        {
            var key = $"ekub.monthly_fee_{currency.ToLowerInvariant()}";
            if (!configs.TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json)) return 0m;
            var trimmed = json.Trim('"');
            return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        var groups = await _db.EkubGroups
            .Where(g => g.Status == EkubGroupStatus.Active)
            .ToListAsync(context.CancellationToken);

        var processed = 0;
        var recorded = 0;
        var skipped = 0;
        var now = DateTime.UtcNow;

        foreach (var g in groups)
        {
            processed++;
            var alreadyRecorded = await _db.EkubFees
                .AnyAsync(f => f.GroupId == g.Id && f.Period == period, context.CancellationToken);
            if (alreadyRecorded) { skipped++; continue; }

            var fee = feeFor(g.Currency);
            if (fee <= 0) { skipped++; continue; }

            _db.EkubFees.Add(new EkubFee
            {
                GroupId = g.Id,
                Period = period,
                Amount = fee,
                Currency = g.Currency,
                TenantId = g.TenantId,
                CreatedAt = now,
                UpdatedAt = now,
            });
            g.LastFeeAppliedAt = now;
            g.UpdatedAt = now;
            recorded++;
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Ekub monthly fee for {Period}: {Recorded} recorded / {Skipped} skipped / {Processed} processed",
            period, recorded, skipped, processed);

        return new ApplyMonthlyFeeResponse
        {
            Success = true,
            Period = period,
            GroupsProcessed = processed,
            FeesRecorded = recorded,
            FeesSkipped = skipped,
        };
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<EkubGroup> LoadGroupAsync(Guid groupId, CancellationToken ct)
    {
        var g = await _db.EkubGroups.FirstOrDefaultAsync(x => x.Id == groupId, ct);
        if (g is null) throw new RpcException(new Status(StatusCode.NotFound, "Group not found."));
        return g;
    }

    private async Task EnsureMemberAsync(Guid groupId, Guid customerId, CancellationToken ct)
    {
        var ok = await _db.EkubMemberships
            .AnyAsync(m => m.GroupId == groupId && m.CustomerId == customerId && m.LeftAt == null, ct);
        if (!ok) throw new RpcException(new Status(StatusCode.PermissionDenied, "Not a member of this group."));
    }

    private async Task<GoldBank.Protos.Common.Money> ComputePotBalanceAsync(Guid groupId, string currency, CancellationToken ct)
    {
        var contributions = await _db.EkubContributions
            .Where(c => c.GroupId == groupId && c.Status == EkubContributionStatus.Confirmed)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var fees = await _db.EkubFees
            .Where(f => f.GroupId == groupId)
            .SumAsync(f => (decimal?)f.Amount, ct) ?? 0m;
        return Money(contributions - fees, currency);
    }

    private async Task<GroupResponse> MapGroupResponseAsync(EkubGroup g, CancellationToken ct)
    {
        var activeMembers = await _db.EkubMemberships
            .CountAsync(m => m.GroupId == g.Id && m.LeftAt == null, ct);

        var resp = new GroupResponse
        {
            Id = g.Id.ToString(),
            Name = g.Name,
            Description = g.Description ?? "",
            Currency = g.Currency,
            MonthlyContribution = g.MonthlyContribution.ToString("F2", CultureInfo.InvariantCulture),
            LoanInterestRatePercent = g.LoanInterestRatePercent.ToString("F3", CultureInfo.InvariantCulture),
            ApplyInterestOnContributions = g.ApplyInterestOnContributions,
            Status = g.Status.ToString(),
            ChairmanCustomerId = g.ChairmanCustomerId.ToString(),
            ActiveMemberCount = activeMembers,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(g.CreatedAt, DateTimeKind.Utc)),
        };
        if (g.ActivatedAt.HasValue)
            resp.ActivatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(g.ActivatedAt.Value, DateTimeKind.Utc));
        return resp;
    }

    private static InvitationResponse MapInvitationResponse(EkubInvitation inv, string groupName) => new()
    {
        Id = inv.Id.ToString(),
        GroupId = inv.GroupId.ToString(),
        GroupName = groupName,
        InviteePhone = inv.InviteePhone,
        InviterCustomerId = inv.InviterCustomerId.ToString(),
        Status = inv.Status.ToString(),
        ExpiresAt = Timestamp.FromDateTime(DateTime.SpecifyKind(inv.ExpiresAt, DateTimeKind.Utc)),
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(inv.CreatedAt, DateTimeKind.Utc)),
    };

    private static ContributionResponse MapContributionResponse(EkubContribution c)
    {
        var resp = new ContributionResponse
        {
            Id = c.Id.ToString(),
            GroupId = c.GroupId.ToString(),
            CustomerId = c.CustomerId.ToString(),
            Amount = Money(c.Amount, c.Currency),
            Period = c.Period,
            Status = c.Status.ToString(),
            Notes = c.Notes ?? "",
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc)),
        };
        if (c.ConfirmedByCustomerId.HasValue)
            resp.ConfirmedByCustomerId = c.ConfirmedByCustomerId.Value.ToString();
        if (c.ConfirmedAt.HasValue)
            resp.ConfirmedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(c.ConfirmedAt.Value, DateTimeKind.Utc));
        return resp;
    }

    private static GoldBank.Protos.Common.Money Money(decimal amount, string currency) => new()
    {
        Amount = amount.ToString("F2", CultureInfo.InvariantCulture),
        Currency = currency,
    };

    private static Guid ParseGuid(string s, string fieldName)
    {
        if (!Guid.TryParse(s, out var g))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Valid {fieldName} is required."));
        return g;
    }
}
