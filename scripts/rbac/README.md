# RBAC helper scripts

Two Azure CLI scripts for provisioning and verifying the identity this extension authenticates
with. They implement the role definitions documented in [rbac.md](../../rbac.md); if you change
one, change both.

Both require the [Azure CLI](https://aka.ms/azure-cli) on `PATH`.

## New-KeyfactorAkvServicePrincipal.ps1

Creates the custom role definitions, an app registration and service principal, and the role
assignments - each at the scope that job type actually needs.

```powershell
./New-KeyfactorAkvServicePrincipal.ps1 `
    -SubscriptionId   <subscription id> `
    -ResourceGroupName <resource group> `
    -Capability        Certificates
```

`-Capability` takes any combination of `Certificates`, `Create` and `Discovery`, or `All`.
Start with `Certificates` - that is all the Inventory, Add and Remove jobs need. Add `Create` only
if operators will create new vaults from Command, and `Discovery` only if you want the extension to
sweep subscriptions for existing vaults.

| Capability | Role created | Assigned at |
| --- | --- | --- |
| `Certificates` | `KeyfactorManageCerts` | resource group |
| `Create` | `KeyfactorVaultCreator` | resource group |
| `Discovery` | `KeyfactorVaultDiscovery` | subscription |

Certificate permissions are assigned at the resource group rather than on individual vaults so that
vaults created later inherit them. This extension does not create role assignments of its own, so a
vault created through Command is otherwise unreachable by the identity that created it.

Run with `-WhatIf` first to see what it would create. You need Owner or User Access Administrator on
the subscription, because creating role definitions and role assignments both require
`Microsoft.Authorization/*/write`.

If an app registration with the same display name already exists it is reused, and the new secret is
**appended** - existing credentials keep working. Pass `-SkipCredential` if the identity already has
a usable secret and you only want the roles created and assigned.

If **more than one** app registration shares the display name, the script stops and lists them
rather than picking one. Microsoft Graph does not return them in a stable order, so choosing would
be arbitrary - and the registration it did not choose is the one whose credentials get touched.
Re-run with `-AppId <application id>` to say which to use:

```powershell
az ad app list --display-name keyfactor-akv --query "[].{name:displayName,appId:appId,created:createdDateTime}" -o table
```

> :warning: `az ad app credential reset` **without** `--append` clears every password and key on an
> app registration. If you issue secrets by hand rather than through this script, always pass
> `--append` unless you intend to revoke the existing ones.

By default the client secret is copied to the clipboard and not printed, so it does not end up in
scrollback, a screenshot or a pasted terminal transcript. Pass `-ShowSecret` to print it instead.
The secret is never written to disk and cannot be retrieved again - issue a new one with
`az ad app credential reset --id <appId> --append`.

> :memo: The clipboard is easily lost - copying anything else, including this script's own output,
> overwrites it. Use the secret before you copy anything else. For an unattended run, capture it
> into a variable instead and never let it reach the clipboard or the console:
>
> ```powershell
> $s = az ad app credential reset --id <appId> --append --query password -o tsv
> ```

## Test-KeyfactorAkvPermissions.ps1

Signs in as the service principal and replays the Azure calls the extension makes, one per
permission, reporting `PASS` or `FAIL` for each. Where a call is denied, Azure's error names the
exact missing action and the script surfaces it.

```powershell
./Test-KeyfactorAkvPermissions.ps1 `
    -SubscriptionId    <subscription id> `
    -ResourceGroupName <resource group> `
    -TenantId          <tenant id> `
    -AppId             <application id> `
    -ClientSecret      <secret> `
    -VaultName         <existing vault>
```

Add `-IncludeCreateVault` to probe vault creation and `-IncludeWriteTests` to probe certificate
import, delete and recover. Both leave their artifacts in place; the cleanup commands are printed at
the end rather than run for you.

The sign-in happens in a temporary `AZURE_CONFIG_DIR`, so your own `az` session is not disturbed.

Pass `-Capability` to match what you actually granted - the same values the provisioning script
takes. The report then distinguishes four outcomes:

| Status | Meaning |
| --- | --- |
| `PASS` | Granted, and the call succeeded. |
| `FAIL` | Granted, but the call was denied - the role is missing something. |
| `n/a` | Not granted, denied as expected. |
| `EXTRA` | **Not granted, but the call succeeded anyway.** |

`EXTRA` is the one to care about. It means the principal holds permissions from somewhere other than
the roles under test - an older assignment, or Reader/Contributor inherited from the subscription or
a management group - and that every `PASS` in the same run is therefore meaningless, because it may
be satisfied by the inherited permission rather than the role you are testing. Find the others with:

```powershell
az role assignment list --assignee <objectId> --all --include-inherited -o table
```

> :warning: Even with no `EXTRA` rows, a `PASS` shows a permission is *sufficient*, not that it is
> *necessary*. Proving necessity means removing an action and watching the probe turn to `FAIL`.

Role assignments take a few minutes to propagate. A `FAIL` immediately after running the creation
script usually just means you were too quick.
