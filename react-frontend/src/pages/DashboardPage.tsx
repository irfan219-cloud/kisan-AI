import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';
import { useNotifications } from '@/hooks/useNotifications';

interface ServiceCard {
  icon: string;
  title: string;
  description: string;
  path: string;
  color: string;
}

interface MetricCard {
  title: string;
  value: string | number;
  change?: number;
  trend: 'up' | 'down' | 'stable';
  icon: string;
}

interface ActivityItem {
  id: string;
  type: 'soil' | 'grading' | 'voice' | 'advisory';
  title: string;
  description: string;
  timestamp: string;
  icon: string;
}

export const DashboardPage: React.FC = () => {
  const { t } = useLanguage();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { notifications } = useNotifications();
  const [recentActivities, setRecentActivities] = useState<ActivityItem[]>([]);
  const [metrics, setMetrics] = useState<MetricCard[]>([]);

  const serviceCards: ServiceCard[] = [
    {
      icon: '🌱',
      title: t('dashboard.soilAnalysis', 'Soil Analysis'),
      description: t('dashboard.soilAnalysisDesc', 'Upload soil health cards for digital analysis'),
      path: '/soil-analysis',
      color: 'from-green-400 to-green-600',
    },
    {
      icon: '⭐',
      title: t('dashboard.qualityGrading', 'Quality Grading'),
      description: t('dashboard.qualityGradingDesc', 'Get quality grades for your produce'),
      path: '/quality-grading',
      color: 'from-yellow-400 to-yellow-600',
    },
    {
      icon: '🎤',
      title: t('dashboard.voiceQueries', 'Voice Queries'),
      description: t('dashboard.voiceQueriesDesc', 'Ask questions in your local dialect'),
      path: '/voice-queries',
      color: 'from-blue-400 to-blue-600',
    },
    {
      icon: '📅',
      title: t('dashboard.plantingAdvisory', 'Planting Advisory'),
      description: t('dashboard.plantingAdvisoryDesc', 'Get personalized planting recommendations'),
      path: '/planting-advisory',
      color: 'from-purple-400 to-purple-600',
    },
    {
      icon: '📊',
      title: t('dashboard.historicalData', 'Historical Data'),
      description: t('dashboard.historicalDataDesc', 'View trends and analytics'),
      path: '/historical-data',
      color: 'from-indigo-400 to-indigo-600',
    },
  ];

  // Load metrics on mount
  useEffect(() => {
    // Mock metrics - in production, fetch from API
    setMetrics([
      {
        title: t('dashboard.metrics.soilTests', 'Soil Tests'),
        value: 12,
        change: 20,
        trend: 'up',
        icon: '🌱',
      },
      {
        title: t('dashboard.metrics.qualityGrades', 'Quality Grades'),
        value: 45,
        change: 15,
        trend: 'up',
        icon: '⭐',
      },
      {
        title: t('dashboard.metrics.voiceQueries', 'Voice Queries'),
        value: 28,
        change: -5,
        trend: 'down',
        icon: '🎤',
      },
      {
        title: t('dashboard.metrics.avgGrade', 'Avg Grade'),
        value: 'A',
        change: 0,
        trend: 'stable',
        icon: '📈',
      },
    ]);

    // Mock recent activities - in production, fetch from API
    setRecentActivities([
      {
        id: '1',
        type: 'soil',
        title: t('dashboard.activity.soilAnalysis', 'Soil Analysis Completed'),
        description: t('dashboard.activity.soilAnalysisDesc', 'pH: 6.5, Organic Carbon: 0.8%'),
        timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
        icon: '🌱',
      },
      {
        id: '2',
        type: 'grading',
        title: t('dashboard.activity.qualityGrading', 'Quality Grading Done'),
        description: t('dashboard.activity.qualityGradingDesc', 'Grade A - ₹45/kg'),
        timestamp: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
        icon: '⭐',
      },
      {
        id: '3',
        type: 'voice',
        title: t('dashboard.activity.voiceQuery', 'Voice Query Answered'),
        description: t('dashboard.activity.voiceQueryDesc', 'Market prices for tomatoes'),
        timestamp: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
        icon: '🎤',
      },
    ]);
  }, [t]);

  const formatTimeAgo = (timestamp: string): string => {
    const now = Date.now();
    const then = new Date(timestamp).getTime();
    const diffMs = now - then;
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffDays = Math.floor(diffHours / 24);

    if (diffHours < 1) {
      return t('dashboard.time.justNow', 'Just now');
    } else if (diffHours < 24) {
      return t('dashboard.time.hoursAgo', `${diffHours} hours ago`);
    } else if (diffDays === 1) {
      return t('dashboard.time.yesterday', 'Yesterday');
    } else {
      return t('dashboard.time.daysAgo', `${diffDays} days ago`);
    }
  };

  const getTrendIcon = (trend: 'up' | 'down' | 'stable'): string => {
    switch (trend) {
      case 'up':
        return '↑';
      case 'down':
        return '↓';
      case 'stable':
        return '→';
    }
  };

  const getTrendColor = (trend: 'up' | 'down' | 'stable'): string => {
    switch (trend) {
      case 'up':
        return 'text-green-600 dark:text-green-400';
      case 'down':
        return 'text-red-600 dark:text-red-400';
      case 'stable':
        return 'text-gray-600 dark:text-gray-400';
    }
  };

  return (
    <div className="space-y-6 sm:space-y-8">
      {/* Welcome Section */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
        className="text-center sm:text-left"
      >
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-800 dark:text-gray-100 mb-2">
          {t('dashboard.welcome', { name: user?.name || t('dashboard.farmer', 'Farmer') })}!
        </h1>
        <p className="text-sm sm:text-base text-gray-600 dark:text-gray-300">
          {t('dashboard.subtitle', 'Access all your farming tools and services')}
        </p>
      </motion.div>

      {/* Metrics Cards */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, delay: 0.1 }}
        className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 lg:gap-6"
      >
        {metrics.map((metric, index) => (
          <motion.div
            key={metric.title}
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ duration: 0.3, delay: 0.1 + index * 0.05 }}
            className="bg-white dark:bg-gray-800 p-4 sm:p-6 rounded-lg shadow-md hover:shadow-lg transition-shadow"
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-2xl sm:text-3xl">{metric.icon}</span>
              {metric.change !== undefined && metric.change !== 0 && (
                <span className={`text-xs sm:text-sm font-semibold ${getTrendColor(metric.trend)}`}>
                  {getTrendIcon(metric.trend)} {Math.abs(metric.change)}%
                </span>
              )}
            </div>
            <div className="text-xl sm:text-2xl lg:text-3xl font-bold text-gray-800 dark:text-gray-100 mb-1">
              {metric.value}
            </div>
            <div className="text-xs sm:text-sm text-gray-600 dark:text-gray-400">
              {metric.title}
            </div>
          </motion.div>
        ))}
      </motion.div>

      {/* Service Cards */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, delay: 0.2 }}
      >
        <h2 className="text-xl sm:text-2xl font-bold text-gray-800 dark:text-gray-100 mb-4">
          {t('dashboard.services', 'Quick Access')}
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4 sm:gap-6">
          {serviceCards.map((card, index) => (
            <motion.button
              key={card.path}
              initial={{ opacity: 0, scale: 0.9 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.3, delay: 0.2 + index * 0.05 }}
              whileHover={{ scale: 1.05, y: -5 }}
              whileTap={{ scale: 0.95 }}
              onClick={() => navigate(card.path)}
              className="bg-white dark:bg-gray-800 p-4 sm:p-6 rounded-lg shadow-md hover:shadow-xl transition-all text-left group relative overflow-hidden"
            >
              {/* Gradient overlay on hover */}
              <div className={`absolute inset-0 bg-gradient-to-br ${card.color} opacity-0 group-hover:opacity-10 transition-opacity`} />
              
              <div className="relative z-10">
                <div className="text-3xl sm:text-4xl mb-3 sm:mb-4">{card.icon}</div>
                <h3 className="text-base sm:text-lg lg:text-xl font-semibold text-gray-800 dark:text-gray-100 mb-2">
                  {card.title}
                </h3>
                <p className="text-xs sm:text-sm text-gray-600 dark:text-gray-300 line-clamp-2">
                  {card.description}
                </p>
              </div>
            </motion.button>
          ))}
        </div>
      </motion.div>

      {/* Recent Activity and Notifications */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-6">
        {/* Recent Activity */}
        <motion.div
          initial={{ opacity: 0, x: -20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.5, delay: 0.3 }}
          className="bg-white dark:bg-gray-800 p-4 sm:p-6 rounded-lg shadow-md"
        >
          <h3 className="text-lg sm:text-xl font-semibold text-gray-800 dark:text-gray-100 mb-4">
            {t('dashboard.recentActivity', 'Recent Activity')}
          </h3>
          {recentActivities.length > 0 ? (
            <div className="space-y-3 sm:space-y-4">
              {recentActivities.map((activity, index) => (
                <motion.div
                  key={activity.id}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.3, delay: 0.3 + index * 0.05 }}
                  className="flex items-start space-x-3 p-3 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
                >
                  <div className="text-2xl sm:text-3xl flex-shrink-0">{activity.icon}</div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm sm:text-base font-medium text-gray-800 dark:text-gray-100 truncate">
                      {activity.title}
                    </p>
                    <p className="text-xs sm:text-sm text-gray-600 dark:text-gray-400 truncate">
                      {activity.description}
                    </p>
                    <p className="text-xs text-gray-500 dark:text-gray-500 mt-1">
                      {formatTimeAgo(activity.timestamp)}
                    </p>
                  </div>
                </motion.div>
              ))}
            </div>
          ) : (
            <p className="text-sm sm:text-base text-gray-600 dark:text-gray-300 text-center py-8">
              {t('dashboard.noActivity', 'No recent activity to display')}
            </p>
          )}
        </motion.div>

        {/* Notifications */}
        <motion.div
          initial={{ opacity: 0, x: 20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.5, delay: 0.3 }}
          className="bg-white dark:bg-gray-800 p-4 sm:p-6 rounded-lg shadow-md"
        >
          <h3 className="text-lg sm:text-xl font-semibold text-gray-800 dark:text-gray-100 mb-4">
            {t('dashboard.notifications', 'Notifications')}
          </h3>
          {notifications.length > 0 ? (
            <div className="space-y-3 sm:space-y-4">
              {notifications.slice(0, 5).map((notification, index) => (
                <motion.div
                  key={notification.id}
                  initial={{ opacity: 0, x: 10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.3, delay: 0.3 + index * 0.05 }}
                  className={`p-3 rounded-lg border-l-4 ${
                    notification.type === 'success'
                      ? 'bg-green-50 dark:bg-green-900/20 border-green-500'
                      : notification.type === 'error'
                      ? 'bg-red-50 dark:bg-red-900/20 border-red-500'
                      : notification.type === 'warning'
                      ? 'bg-yellow-50 dark:bg-yellow-900/20 border-yellow-500'
                      : 'bg-blue-50 dark:bg-blue-900/20 border-blue-500'
                  }`}
                >
                  <p className="text-sm sm:text-base font-medium text-gray-800 dark:text-gray-100">
                    {notification.title}
                  </p>
                  <p className="text-xs sm:text-sm text-gray-600 dark:text-gray-400 mt-1">
                    {notification.message}
                  </p>
                </motion.div>
              ))}
            </div>
          ) : (
            <p className="text-sm sm:text-base text-gray-600 dark:text-gray-300 text-center py-8">
              {t('dashboard.noNotifications', 'No new notifications')}
            </p>
          )}
        </motion.div>
      </div>
    </div>
  );
};
