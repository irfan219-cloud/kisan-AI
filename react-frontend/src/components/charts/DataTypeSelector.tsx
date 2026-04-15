import React from 'react';
import { TrendingUp, Leaf, Award, BarChart3 } from 'lucide-react';

type DataType = 'prices' | 'soilHealth' | 'qualityGrades' | 'all';

interface DataTypeSelectorProps {
  selectedType: DataType;
  onTypeChange: (type: DataType) => void;
}

export const DataTypeSelector: React.FC<DataTypeSelectorProps> = ({
  selectedType,
  onTypeChange,
}) => {
  const dataTypes: { value: DataType; label: string; icon: React.ReactNode; description: string }[] = [
    {
      value: 'all',
      label: 'All Data',
      icon: <BarChart3 className="w-5 h-5" />,
      description: 'View all historical data',
    },
    {
      value: 'prices',
      label: 'Market Prices',
      icon: <TrendingUp className="w-5 h-5" />,
      description: 'Price trends over time',
    },
    {
      value: 'soilHealth',
      label: 'Soil Health',
      icon: <Leaf className="w-5 h-5" />,
      description: 'Soil nutrient levels',
    },
    {
      value: 'qualityGrades',
      label: 'Quality Grades',
      icon: <Award className="w-5 h-5" />,
      description: 'Produce quality history',
    },
  ];

  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-4">
      <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">
        Data Type
      </h3>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
        {dataTypes.map((type) => (
          <button
            key={type.value}
            onClick={() => onTypeChange(type.value)}
            className={`p-4 rounded-lg border-2 transition-all ${
              selectedType === type.value
                ? 'border-blue-600 bg-blue-50 dark:bg-blue-900/20'
                : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
            }`}
          >
            <div className="flex flex-col items-center text-center space-y-2">
              <div
                className={`${
                  selectedType === type.value
                    ? 'text-blue-600 dark:text-blue-400'
                    : 'text-gray-600 dark:text-gray-400'
                }`}
              >
                {type.icon}
              </div>
              <div>
                <p
                  className={`text-sm font-semibold ${
                    selectedType === type.value
                      ? 'text-blue-600 dark:text-blue-400'
                      : 'text-gray-900 dark:text-gray-100'
                  }`}
                >
                  {type.label}
                </p>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {type.description}
                </p>
              </div>
            </div>
          </button>
        ))}
      </div>
    </div>
  );
};
