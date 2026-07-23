"use client"

import { forwardRef, useCallback, type HTMLAttributes } from "react"
import { MapPin, Star, MessageSquare, BadgeCheck } from "lucide-react"

import { cn } from "@/lib/utils"
import { Card } from "./card"
import { Badge } from "./badge"
import { Button } from "./button"
import { Avatar, AvatarFallback, AvatarImage } from "./avatar"

export interface UserAvatarCardProps extends HTMLAttributes<HTMLDivElement> {
  name: string
  role?: string
  skills?: string[]
  rating?: number
  location?: string
  avatarUrl?: string
  available?: boolean
  isVerified?: boolean
  onMessage?: () => void
  maxSkills?: number
}

const UserAvatarCard = forwardRef<HTMLDivElement, UserAvatarCardProps>(
  (
    {
      className,
      name,
      role,
      skills = [],
      rating,
      location,
      avatarUrl,
      available = false,
      isVerified = false,
      onMessage,
      maxSkills = 4,
      ...props
    },
    ref
  ) => {
    const initials = name
      .split(" ")
      .map((n) => n[0])
      .join("")
      .toUpperCase()
      .slice(0, 2)

    const displayedSkills = skills.slice(0, maxSkills)
    const remainingSkills = skills.length - maxSkills

    const handleMessage = useCallback((e: { stopPropagation(): void }) => { e.stopPropagation(); onMessage?.() }, [onMessage])

    return (
      <Card
        ref={ref}
        variant="elevated"
        hover="lift"
        className={cn("cursor-pointer", className)}
        {...props}
      >
        <div className="p-5">
          <div className="mb-4 flex items-start gap-4">
            <Avatar className="h-14 w-14 flex-shrink-0">
              <AvatarImage src={avatarUrl} alt={name} />
              <AvatarFallback className="bg-gradient-card-blue text-lg font-bold text-white">
                {initials}
              </AvatarFallback>
            </Avatar>

            <div className="min-w-0 flex-1">
              <h3 className="flex items-center gap-1 font-bold text-foreground">
                {name}
                {isVerified && <BadgeCheck className="h-4 w-4 text-blue-500 shrink-0" />}
              </h3>
              {role && <p className="mb-2 text-sm text-muted-foreground">{role}</p>}

              <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                {rating !== undefined && (
                  <div className="flex items-center gap-1">
                    <Star className="h-3.5 w-3.5 fill-yellow-500 text-yellow-500" />
                    <span>{rating.toFixed(1)}</span>
                  </div>
                )}
                {location && (
                  <div className="flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5" />
                    <span>{location}</span>
                  </div>
                )}
              </div>
            </div>

            {available && (
              <Badge variant="active" className="flex-shrink-0">
                Available
              </Badge>
            )}
          </div>

          {displayedSkills.length > 0 && (
            <div className="mb-4 flex flex-wrap gap-2">
              {displayedSkills.map((skill) => (
                <Badge key={skill} variant="info" className="text-xs">
                  {skill}
                </Badge>
              ))}
              {remainingSkills > 0 && (
                <span className="px-2 py-0.5 text-xs text-muted-foreground">
                  +{remainingSkills}
                </span>
              )}
            </div>
          )}

          {onMessage && (
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              onClick={handleMessage}
            >
              <MessageSquare className="mr-2 h-4 w-4" />
              Message
            </Button>
          )}
        </div>
      </Card>
    )
  }
)
UserAvatarCard.displayName = "UserAvatarCard"

export { UserAvatarCard }
