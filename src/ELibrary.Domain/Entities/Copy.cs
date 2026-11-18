using ELibrary.Domain.Common;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

/// <summary>A physical item of a title. The methods below are the only legal status transitions.</summary>
public class Copy : Entity
{
    private Copy()
    {
        Barcode = string.Empty;
        Location = string.Empty;
    }

    public Copy(int titleId, string barcode, string location)
    {
        TitleId = titleId;
        Barcode = Guard.Code(barcode, "Barcode", 30);
        Location = Guard.Required(location, "Location", 50);
        Status = CopyStatus.Available;
    }

    public int TitleId { get; private set; }

    public string Barcode { get; private set; }

    public string Location { get; private set; }

    public CopyStatus Status { get; private set; }

    public bool IsAvailable => Status == CopyStatus.Available;

    public void ChangeLocation(string location) => Location = Guard.Required(location, "Location", 50);

    /// <summary>Available → OnLoan.</summary>
    public void CheckOut()
    {
        EnsureStatus(CopyStatus.Available, "lent");
        Status = CopyStatus.OnLoan;
    }

    /// <summary>OnHold → OnLoan. The caller must already have checked that the borrower is the holder (BR6).</summary>
    public void CheckOutHeldCopy()
    {
        EnsureStatus(CopyStatus.OnHold, "lent from the hold shelf");
        Status = CopyStatus.OnLoan;
    }

    /// <summary>OnLoan → Available (the hold allocator may then put it on hold).</summary>
    public void CheckIn()
    {
        EnsureStatus(CopyStatus.OnLoan, "returned");
        Status = CopyStatus.Available;
    }

    /// <summary>Available → OnHold (BR5).</summary>
    public void PlaceOnHold()
    {
        EnsureStatus(CopyStatus.Available, "put on hold");
        Status = CopyStatus.OnHold;
    }

    /// <summary>OnHold → Available, when a hold expires or is cancelled.</summary>
    public void ReleaseHold()
    {
        EnsureStatus(CopyStatus.OnHold, "released from hold");
        Status = CopyStatus.Available;
    }

    /// <summary>
    /// Manual change by a librarian (repair, lost, withdrawn, back to available). Copies that are on
    /// loan or on hold must go through circulation instead.
    /// </summary>
    public void ChangeStatus(CopyStatus newStatus)
    {
        if (Status is CopyStatus.OnLoan or CopyStatus.OnHold)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule,
                $"Copy {Barcode} is {Status}; return it or release the hold first.");
        }

        if (newStatus is CopyStatus.OnLoan or CopyStatus.OnHold)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule,
                "Use Check out or the reservation queue to set a copy on loan or on hold.");
        }

        Status = newStatus;
    }

    private void EnsureStatus(CopyStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule,
                $"Copy {Barcode} is {Status} and cannot be {action}.");
        }
    }
}
