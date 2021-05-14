SELECT COUNT(*) AS TotalTrades FROM tradeStatistics3Store 
WHERE Id IN 
(
	SELECT MIN(Id) AS Id FROM tradeStatistics3Store 
	GROUP BY Hash 
)
AND Date < @DateArg