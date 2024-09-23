-- migrate max WalletPosition from deposit endpoints to IdIncrements
INSERT INTO IdIncrements (id, number)
SELECT WalletID, Max(WalletPosition) AS MaxPosition
FROM DepositEndpoints
WHERE WalletID IS NOT NULL
GROUP BY WalletID
ON CONFLICT (id) DO UPDATE SET number = EXCLUDED.number;

-- migrate max DepositEndpointPosition from slices to IdIncrements
INSERT INTO IdIncrements (id, number)
SELECT DepositEndpointId, Max(DepositEndpointPosition) AS MaxPosition
FROM Slices
GROUP BY DepositEndpointId
ON CONFLICT (id) DO UPDATE SET number = EXCLUDED.number;
