# TDious

* Edit multiple DevOps tasks at once, optionally save comments in Task Description, and see total hours completed for the day.
    * The task-hours inputs are debounced by 3 seconds, meaning you have 3 seconds to finish updating the hours of a given task before a modal is launched (if comments are enabled—otherwise, it'll automatically be saved).
* Edit custom time-tracking fields (currently fields are hardcoded to Custom.IssueResearchTime, Custom.MeetingsTime, Custom.SupportTime, Custom.ItemReviewTime, Custom.RequirementsTime, Custom.InfrastructureTime, and Custom.UncategorizedTime)
* Create child tasks (copies title, description, area path, iteration path for a new child task)
    * Includes option to set 'CurrentEstimate'

## Installation
* Double-click the TDious_0.0.10.0_x64.cer file
* Click the Install Certificate... button
* Store Location -> Local Machine
* Select the Place all certificates in the following store
* Click Browse... button
* Select the Trusted Root Certification Authorities
* OK
* Double-click TDious_0.0.10.0_x64.msix
* Continue through the installer

## Setup

* Navigate to the settings tab.
* Enter your organization's DevOps URL for the 'DevOps Endpoint' setting.
* Enter your personal access token for the 'DevOps Personal Access Token' setting.
* For 'DevOps Email', enter the email that your DevOps account is associated to.
* If you want to use the 'Create Tasks' feature, you'll need to add a 'Items to Create List Wiql'

### Generating a DevOps Personal Access Token
At the time of writing:
* Select the 'User Settings' button at the top-right of DevOps.
* Select 'Personal Access Tokens' from the dropdown.
* Select 'New Token' from the top-right.
* Give the token a name reflecting its use (such as 'TDious Access Token')
* Set a reasonable expiration date (perhaps one year)
* Ensure that, under 'Work Items,' the 'Read, write, & manage' checkbox is selected.
* Select 'Create'
* Copy the generated token to the clipboard and back it up just in case this potentially buggy app has an issue

## TODOs
* Use last modified from DevOps for calculating the total hours completed instead of relying on local cache
* Replace all that janky code
* Make UI fancier (it's just the default .NET Maui Blazor Hybrid app template)
* Displaying errors to the user
* Figure out why the program crashes at launched for some users
* CSV app setting for time-tracking fields instead of hardcoding for a specific use-case
* Refresh on wake-up instead of having to manually refresh/navigate to window
