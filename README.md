# Where2Play
A smart concert booking assistant that helps bands and tour managers find the best cities to play using historical concert data and artist popularity.

## Introduction

Booking tours is an expensive and challenging process. As a musical artist, it can be challenging to know what cities have audiences that want to come out and watch your shows. where2play helps by:

- Finding what cities draw the biggest crowds
- Showing you how many fans of your genre are in a specified area
- Determining which venues would be a good fit based on your audience size

By utilizing industry data, you will be able to ensure that you book the best venues around the world, playing shows for your most engaged audiences.

## Logo

![Logo](https://github.com/pkkenn/Where2Play/blob/master/Where2Play/wwwroot/images/logo.jpg?raw=true)

## Storyboard

## Projects

[Open Projects](https://github.com/pkkenn/Where2Play/projects?query=is%3Aopen)

## Requirements

### Requirement 001 Search for City

#### Scenario

As a user, I want to view historical concerts in a selected city.

#### Dependencies

Concert data is available.

#### Examples

##### 1.1

**Given** historical concert data is available \
**When** I search for "Cleveland" \
**Then** I should receive a list with results similar to the following: \
\
Artist: Taylor Swift \
Genre: Pop \
Popularity: 100 \
Date: 5/7/2025 \
Venue: Rocket Arena

##### 1.2

**Given** historical concert data is available \
**When** I search for "Little Diomede" \
**Then** I should receive an empty list

### Requirement 002 Find Similar Artists

#### Scenario

As a user, I want to search for artists by genre and popularity

#### Dependencies

Artist data is available.

#### Examples

##### 2.1

**Given** current artist information is available \
**When** I search for select the genre of "Funk" and the popularity "Moderately Famous" \
**Then** I should receive a list with results similar to the following: \
\
Artist: Vulfpeck

Artist: Couch

### Requirement 003 Recommend Best Cities

#### Scenario

As a user, I want to see which cities are most promising for future concerts based on fan engagement.

#### Dependencies

Historical concert data and listener data (Spotify, ticketmaster) are available. 

#### Examples

#### 3.1

**Given** an artist with strong engagment in the Midwest \
**When** I request city recommendations \
**Then** I should see cities like Chicago, Detroit, and Minneapolis ranked by audience population.

### Requirement 004 Match Venues to Artist Popularity

#### Scenario

As a user, I want to find venues in a city that match my expected audience size and genre.

#### Dependencies

Venue capacity, location, and genre compatibility data are available.

#### Examples

##### 4.1

**Given** I expect 5,000 attendees for a show in "Atlanta" \
**When** I search for venues in "Atlanta" \
**Then** I should see results like "Coca-Cola Roxy" or "Tabernacle" ranked by best fit.


## Data Sources

1. [Spotify](https://developer.spotify.com/documentation/web-api)
2. [Ticketmaster](https://developer.ticketmaster.com/products-and-docs/apis/discovery-api/v2/)

## Team Composition

### Team Members

- Precious Adugyamfi
- Kenneth Alexis
- David Dickens
- Thomas Girgash
- Thi Nguyen

### Meeting Cadence
- **Day:** Sunday
- **Time:** 9:00 PM EST
- **Format:** Virtual (Zoom/Teams)
- **Purpose:** Review progress, assign tasks for the week, and demo new features.
