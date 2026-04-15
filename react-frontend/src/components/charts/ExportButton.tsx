import React, { useState } from 'react';
import { Download, FileText, Table } from 'lucide-react';
import { exportToPDF, exportToCSV } from '@/utils/dataExport';
import { ChartData, ComparisonChartData, TrendLineData } from '@/types/chartData';

interface ExportButtonProps {
  title: string;
  chartData?: ChartData;
  comparisonData?: ComparisonChartData;
  trendData?: TrendLineData;
  insights?: string[];
  metadata?: {
    dateRange: string;
    dataType: string;
    generatedAt: string;
  };
}

export const ExportButton: React.FC<ExportButtonProps> = ({
  title,
  chartData,
  comparisonData,
  trendData,
  insights,
  metadata,
}) => {
  const [showMenu, setShowMenu] = useState(false);
  const [exporting, setExporting] = useState(false);

  const handleExport = async (format: 'pdf' | 'csv') => {
    setExporting(true);
    setShowMenu(false);

    try {
      const exportData = {
        title,
        chartData,
        comparisonData,
        trendData,
        insights,
        metadata,
      };

      if (format === 'pdf') {
        await exportToPDF(exportData);
      } else {
        exportToCSV(exportData);
      }
    } catch (error) {
      console.error('Export failed:', error);
      alert('Failed to export data. Please try again.');
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="relative">
      <button
        onClick={() => setShowMenu(!showMenu)}
        disabled={exporting}
        className="flex items-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        <Download className="w-4 h-4" />
        <span className="text-sm font-medium">
          {exporting ? 'Exporting...' : 'Export'}
        </span>
      </button>

      {showMenu && (
        <>
          {/* Backdrop */}
          <div
            className="fixed inset-0 z-10"
            onClick={() => setShowMenu(false)}
          />

          {/* Menu */}
          <div className="absolute right-0 mt-2 w-48 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 z-20">
            <div className="py-1">
              <button
                onClick={() => handleExport('pdf')}
                className="w-full flex items-center space-x-3 px-4 py-3 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
              >
                <FileText className="w-4 h-4" />
                <span>Export as PDF</span>
              </button>
              <button
                onClick={() => handleExport('csv')}
                className="w-full flex items-center space-x-3 px-4 py-3 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
              >
                <Table className="w-4 h-4" />
                <span>Export as CSV</span>
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
};
