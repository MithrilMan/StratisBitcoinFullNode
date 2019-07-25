using NBitcoin;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// Wallet repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface IWalletRepository : IRepositoryBase<long, IWallet>
    {
        IWallet GetByName(string walletName);

        IWallet SetWalletTip(string walletName, ChainedHeader tip);
    }
}
