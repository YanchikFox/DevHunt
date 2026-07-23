/**
 * Represents a user profile.
 */
export interface UserProfile {
  /** Unique identifier of the user */
  id: string
  /** Email address of the user */
  email: string
  /** Role of the user (e.g., Developer, Designer) */
  role: string
  /** Full name of the user */
  fullName?: string
  /** Biography or description of the user */
  bio?: string
  /** Timezone of the user */
  timezone?: string
  /** List of skills possessed by the user */
  skills?: string[]
  /** Years of experience */
  experience?: number
  /** User rating (0-5) */
  rating?: number
  /** URL to the user's avatar image */
  avatarUrl?: string
  /** Whether the user is verified */
  isVerified: boolean
  /** Whether the user account is active */
  isActive: boolean
  /** Timestamp when the account was created */
  createdAt: string
  /** Timestamp when the profile was last updated */
  updatedAt?: string
  /** Timestamp of the last login */
  lastLogin?: string
  /** Preferred language of the user */
  language?: string
  /** Unique handle chosen by the user (e.g. @cool_dev42) */
  username?: string | null
  /** GitHub profile URL or username */
  github?: string
  /** GitHub username (login) for API operations like issue assignment */
  githubUsername?: string
  /** LinkedIn profile URL or username */
  linkedin?: string
  /** Personal website URL */
  website?: string
  /** Visibility level for profile relations */
  relationVisibility?: string
  /** Whether the current viewer is following this user */
  isFollower?: boolean
  /** Whether this user is followed by the current viewer */
  isFollowing?: boolean
  /** Activity feed visibility preference */
  activityVisibility?: string
  /** Count of followers */
  followersCount?: number
  /** Count of following */
  followingCount?: number
  /** Count of projects */
  projectsCount?: number
  /** Optional preview of projects */
  projects?: { id: string; title?: string; description?: string }[]
}

/**
 * Data required to update a user profile.
 */
export interface UpdateProfileData {
  /** Full name of the user */
  fullName?: string
  /** Biography or description */
  bio?: string
  /** Timezone */
  timezone?: string
  /** List of skills */
  skills?: string[]
  /** Years of experience */
  experience?: number
  /** Unique handle (null = clear, omit = no change) */
  username?: string | null
  /** GitHub profile URL or username */
  github?: string
  /** GitHub username (login) for API operations */
  githubUsername?: string
  /** LinkedIn profile URL or username */
  linkedin?: string
  /** Personal website URL */
  website?: string
  /** Avatar URL */
  avatarUrl?: string
  /** Preferred language */
  language?: string
}

/**
 * Privacy settings for a user profile.
 */
export interface PrivacySettings {
  /** Visibility of the profile (e.g., public, private) */
  profileVisibility: string
  /** Whether to show the email address */
  showEmail: boolean
  /** Whether to show skills */
  showSkills: boolean
  /** Whether to show experience */
  showExperience: boolean
  /** Whether to show rating */
  showRating: boolean
  /** Whether to show projects */
  showProjects: boolean
  /** Whether to show social links */
  showSocialLinks: boolean
  /** Whether to show achievements */
  showAchievements: boolean
  /** Whether to allow searching by email */
  allowEmailSearch: boolean
  /** Whether to notify on new messages */
  notifyOnMessages: boolean
  /** Whether to notify on invitations */
  notifyOnInvitations: boolean
  /** Visibility of the activity feed */
  activityVisibility?: string
}

/**
 * Represents an item in the user activity feed.
 */
export interface UserActivityFeedItem {
  /** Unique identifier of the activity */
  id: string
  /** Type of the event */
  eventType: string
  /** Group of the event */
  eventGroup?: string
  /** Summary of the activity */
  summary: string
  /** Visibility of the activity */
  visibility: string
  /** ID of the actor who performed the activity */
  actorId: string
  /** Name of the actor */
  actorName: string
  /** Avatar URL of the actor */
  actorAvatarUrl?: string
  /** ID of the target user (if applicable) */
  targetUserId?: string
  /** Name of the target user (if applicable) */
  targetUserName?: string
  /** ID of the project (if applicable) */
  projectId?: string
  /** Title of the project (if applicable) */
  projectTitle?: string
  /** Timestamp when the activity occurred */
  createdAt: string

  /** Optional payload JSON for richer rendering (e.g., task details) */
  payloadJson?: string

  // Enriched news fields (populated by feed endpoint)
  /** News post ID for news events */
  newsPostId?: string
  /** Full news title */
  newsTitle?: string
  /** Full news content */
  newsContent?: string
  /** Likes count */
  likesCount?: number
  /** Comments count */
  commentsCount?: number
  /** Whether current user liked this post */
  isLikedByCurrentUser?: boolean
  /** Resolved news attachments */
  newsAttachments?: Array<{
    type: string
    fileId?: string
    url?: string
    fileName?: string
    contentType?: string
    downloadUrl?: string
  }>
}

/**
 * Metadata for pagination.
 */
export interface PaginationMetadata {
  /** Current page number */
  page: number
  /** Number of items per page */
  pageSize: number
  /** Total number of items */
  total: number
  /** Total number of pages */
  totalPages: number
  /** Whether there is a next page */
  hasNext: boolean
  /** Whether there is a previous page */
  hasPrevious: boolean
}

/**
 * Represents a page of user activity feed items.
 */
export interface UserActivityFeedPage {
  /** List of activity feed items */
  data: UserActivityFeedItem[]
  /** Pagination metadata */
  pagination: PaginationMetadata
}

/**
 * Statistics for a user.
 */
export interface UserStatsDto {
  /** Number of followers */
  followersCount: number
  /** Number of users followed */
  followingCount: number
  /** Number of projects */
  projectsCount: number
  /** Number of recent activities */
  recentActivityCount: number
  /** Contribution score */
  contributionScore: number
  /** Community score */
  communityScore: number
  /** Number of consecutive days active in the last 14 days */
  consistencyDaysActiveLast14: number
}

/**
 * Represents a suggested user to follow.
 */
export interface SuggestedUser {
  /** Unique identifier of the user */
  id: string
  /** Name of the user */
  name: string
  /** Avatar URL of the user */
  avatarUrl?: string | null
  /** Number of mutual projects */
  mutualProjectsCount: number
  /** Score based on recent activity */
  recentActivityScore: number
  /** Whether the user is verified */
  isVerified?: boolean
}
