# Issues on current wallet implementation

### Stratis.Bitcoin.Features.Wallet

##### SpecialPurposeAccountIndexesStart

`Wallet.SpecialPurposeAccountIndexesStart` is used to set a limit to "normal" accounts, reserving
indexes with higher value for special purpose.

The problem is that there isn't a reliable way to reserve an index for special purpose, theoretically
any feature can reuse a special index used by another feature. 

e.g. ColdStaking defines
`const int ColdWalletAccountIndex = Wallet.Wallet.SpecialPurposeAccountIndexesStart + 0;`

If another feature try to reuse the same index, coldstaking would not be compatible with such feature without having a clear evidence of the problem.

A proposed solution to this, is implement a method to register special purpose account indexes for this need and if multiple features try to use the same index, an exception should be thrown during the feature initialization.

An alternative solution is to not rely just on indexes but on account names too, we shouldn't be forced to name them "account n", did we? This way we may have a different account name for each feature requiring custom accounts, without having to rely on indexes (we may add a constraint that custom account names have their index starting from `SpecialPurposeAccountIndexesStart`  so it will even be backward compatible).



##### GetUnusedAccount

`WalletManager.GetUnusedAccount` returns the first unused account, if available, or create a new one.
Its usage is not clear and comment didn't describe this behavior.
It's used in `WalletController` to act against the `CreateNewAccount` making this method confusing but at least on controller the comment is right.
WalletManager should have this behavior be splitted in 2 different methods (GetUnused and CreateNewAccount) or should implement the whole logic in a new CreateNewAccount  that will have to reflect the controller comment to be BIP44 compliant (returning an unused account if available before creating a new one).
Side note: in [BIP 44](https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki) it's stated:

> Software should prevent a creation of an account if a previous account does not have a transaction history (meaning none of its addresses have been used before).

And there is a description about Account discovery. Reading these descriptions it's clear that to get a new account the node should be synced to the tip, otherwise we may return an account that at the time of creation didn't had transaction history just because our node was in IBD and transactions for that account didn't took place yet. (Current implementation didn't account for that).



##### Wallets are all in memory

Currently our wallet implementation loads all the wallets a user has, straight in memory.

While this may be ok for a standard user use case that have to deal with few transactions, it may be a problem for advanced use cases.

We need to implement a wallet that fetches data from the storage by request and makes use of a cache layer to speedup common operations.

It may have an internal state of updated balances for known addresses and may have a cache of UTXOs that may be used.



##### Wallet class doesn't account for modularity

Wallet class doesn't account for modularity, it needs to implement a common interface that we can use instead of wallet instances outside of wallet feature, this way we can then have custom wallet implementations that improve the wallet model itself but ensure the common functionality are implemented. 



##### Wallet Locking/Unlocking

Bitcoin has a concept that's wallet locking/unlocking, this allows you to unlock a wallet for a specified amount of time so you can then do operation on its addresses and if you don't unlock an encrypted wallet, you can't perform any RPC operation on the wallet that involve spending or exporting keys.

We have a fake implementation of it, in the sense that we have LockWallet and UnlockWallet methods even on controller, and they store the secrets in a cache that lasts for a specified time span, but we don't really use the mechanism to perform operations on these addresses, because where we need to do operations on keys, we pass the password to the methods, recreating the secrets if we doesn't found them in the cache.
Basically we don't implement any timespan unlocking mechanism at all.
We should instead (if we want to implement the wallet similar to Bitcoin) allow certain operations only on unlocked wallets, so if we don't find the secret in the cache (that we manage already, so that's weird, like having an half done implementation) then we'll throw.



##### Lock on wallet operations

Wallet Controller previously was calling WalletManager for many of its operation and it was using a lockObject to perform some operation like GetUnusedAccount.
The problem that it tried to solve was that if an operation of account creation happens simultaneously, it may lead to problems or race conditions.
What I see as a problem was that the lock was at WalletManager level (was locking on `lockObject` variable) while it should be at wallet level.
At the moment I'm removing every lock we are using, we'll reintroduce locking once the new design is settled down.



---

Repository reference for implementations: https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/

old feature implementation: [Stratis.Bitcoin.Features.Wallet](https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/src/Stratis.Bitcoin.Features.Wallet)
new one: [Stratis.Features.Wallet](https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/src/Stratis.Features.Wallet)