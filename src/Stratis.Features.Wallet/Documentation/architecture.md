# Stratis.Features.Wallet Architecture

[TOC]

------

## Introduction

This feature acts as a foundation for specific wallet implementations that may follow, so one of the goal is to be extensible as much as possible.

Previous wallet was implemented using a single JSON document that was updated frequently and everything was kept in memory, this caused of course scalability issues that the new feature aim to fix.

In addition, previous wallet feature was used from other projects so we tried to keep the interface layer as close as possible to external uses (other features), even if this has not be possible at maximum extent.

Previously the Wallet was used by other feature and it had no interface, while now we have a `IWallet` that export the bare minimum required data, hiding internal implementation details.



## Previous wallet design (Stratis.Bitcoin.Features.Wallet)

In the previous wallet feature (Stratis.Bitcoin.Features.Wallet) the main components are:

- [Wallet](#Wallet)
- [WalletManager](#WalletManager)
- [WalletController](#WalletController)
- [WalletSyncManager](#WalletSyncManager)

#### Wallet

This class represents the Wallet model fully. It comprehends a hierarchy of `HDAccounts` each one with two lists of HDAddresses: one for internal uses (change addresses) and one for external uses (the address used to receive funds from other people).

Each one of the inner classes implements their own methods to query data related to them or to persist changes.

Basically this design doesn't implement any separation of concern and the wallet implements most of the funtionality like persistence, repository, query and so on.

It doesn't implement any interface so this object was used directly from any party interested in using the wallet feature.

#### WalletManager

This component is responsible for different tasks: 

- loads the needed wallets when the node starts.

- keeps loaded wallets in sync with the chain in conjunction with `WalletSyncManager`.

- acts between `WalletController` and Wallet, wrapping some of the feature exposed by the wallet itself as public accessible methods that the `WalletController` uses when it doesn't use directly the Wallet.

- keeps track of unlocked wallet using a `MemoryCache` (privateKeyCache var) that lasts some time.

- keeps an in memory dictionary of known wallet addresses to speedup the lookup.

  ```c#
  // In order to allow faster look-ups of transactions affecting the wallets' addresses,
  // we keep a couple of objects in memory:
  // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
  // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
  // 3. a mapping of all inputs with their corresponding transactions, to facilitate rapid lookup
  private Dictionary<OutPoint, TransactionData> outpointLookup;
  internal ScriptToAddressLookup scriptToAddressLookup;
  private Dictionary<OutPoint, TransactionData> inputLookup;
  ```

#### WalletController

Component that exposes endpoints to the API Feature, allowing swagger to be used to perform operation on the wallet.

#### WalletSyncManager

Works in synergy with WalletManager to keep the wallets updated with the chain.

Contains the logic to perform a wallet resync, starting back in time respect to current chain tip.



------



## New Wallet design (Stratis.Features.Wallet)

<u>**WARNING: this is a work in progress - can contains invalid information**</u>

##### IWallet

Interfaces that's used to pass wallet informations without disclosing internal details to external users.
It contains the core information common to any custom Wallet implementation.

#### WalletService

Implementation of IWalletService, used to interact with the wallet.

It's used by the WalletController and implement the actions required by defined use cases.

#### WalletController

Component that exposes endpoints to the API Feature, allowing swagger to be used to perform operation on the wallet.

Internally it makes use of IWalletService to interact with wallets.